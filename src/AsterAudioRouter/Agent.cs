// Agent 主循环（每座位运行一个实例）：
//   1. 加载配置（exe 同目录 config.json 优先，其次 %ProgramData%\AsterAudioRouter\config.json），
//      当前用户名精确匹配，未命中走 * 通配兜底；FileSystemWatcher 监听配置变化热重载。
//   2. 每 2 秒扫描本会话所有活动端点的音频会话 → 对"有音频会话但尚未路由"的进程执行路由。
//   3. 每 30 秒对已路由进程全量重申一次（应对设备重连、用户手改设置）。
//   4. 跳过自身进程、系统进程（0/4）、系统音效会话（0xFFFFFFFF）。
using AsterAudioRouter.Interop;
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsterAudioRouter;

public sealed class Agent : IDisposable
{
    private readonly bool _dryRun;
    private readonly string _configPath;
    private readonly Dictionary<uint, int> _attempts = new();
    private readonly HashSet<uint> _routed = new();
    private readonly HashSet<string> _overrideSkippedLogged = new(StringComparer.OrdinalIgnoreCase);
    private readonly EventWaitHandle _reloadEvent;
    private readonly OverridesStore _overrides;
    private FileSystemWatcher? _watcher;
    private ResolvedRoute? _route;
    private string? _matchedRule;
    private int _overrideDirty; // 1 = 覆盖文件已变化，需要清空路由记录重新评估

    public Agent(string exeDir, bool dryRun, OverridesStore overrides)
    {
        _dryRun = dryRun;
        _overrides = overrides;
        _overrides.Changed += () => Interlocked.Exchange(ref _overrideDirty, 1);
        _configPath = RouterConfig.FindConfigPath(exeDir);
        _reloadEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "OYAAsterAudioMixerReload");
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _watcher = new FileSystemWatcher(dir, Path.GetFileName(_configPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };
                _watcher.Changed += (_, _) => _reloadEvent.Set();
                _watcher.Created += (_, _) => _reloadEvent.Set();
                _watcher.EnableRaisingEvents = true;
            }
        }
        catch
        {
            // 无法监听配置目录时仍可运行（--reload 手动触发兜底）
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _reloadEvent.Dispose();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        AgentLogger.Log($"agent 启动{(_dryRun ? "（dry-run：只记录不执行）" : "")}，配置: {_configPath}");
        await ReloadAsync();

        var nextFullReassert = DateTime.UtcNow.AddSeconds(30);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_reloadEvent.WaitOne(0))
                {
                    AgentLogger.Log("检测到配置变化，重新加载");
                    await ReloadAsync();
                }

                if (Interlocked.Exchange(ref _overrideDirty, 0) == 1)
                {
                    // 手动覆盖有增删（用户在合成器改"自动/手动"）：清空已路由记录，
                    // 下一轮扫描立即按新覆盖状态重新路由（≤2 秒，不必等 30 秒重申）
                    _routed.Clear();
                    _attempts.Clear();
                    AgentLogger.Log("手动覆盖变化，重新评估全部路由");
                }

                Sweep();

                if (DateTime.UtcNow >= nextFullReassert)
                {
                    ReassertAll();
                    nextFullReassert = DateTime.UtcNow.AddSeconds(30);
                }
            }
            catch (Exception ex)
            {
                AgentLogger.Log($"循环异常: {ex.Message}");
            }
            await Task.Delay(2000, ct);
        }
        AgentLogger.Log("agent 退出");
    }

    private async Task ReloadAsync()
    {
        _routed.Clear();
        _attempts.Clear();
        _route = null;
        try
        {
            var entries = RouterConfig.Load(_configPath);
            var userName = Environment.UserName;
            var entry = RouterConfig.Match(entries, userName);
            if (entry == null)
            {
                AgentLogger.Log($"配置已加载，用户 {userName} 无匹配规则，agent 空闲");
                return;
            }
            _matchedRule = entry.User;
            _route = await RouterConfig.ResolveAsync(entry);
            AgentLogger.Log($"配置已加载，用户 {userName} 命中规则 user={entry.User}：playback={entry.Playback} capture={entry.Capture}");
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"配置加载失败: {ex.Message}");
        }
    }

    private void Sweep()
    {
        if (_route == null || (_route.PlaybackSwdId == null && _route.CaptureSwdId == null))
            return;

        var selfPid = (uint)Process.GetCurrentProcess().Id;
        foreach (var pid in AudioSessionScanner.GetActiveSessionPids())
        {
            if (pid == selfPid || pid == 0 || pid == 4 || pid == 0xFFFFFFFF)
                continue;
            if (_routed.Contains(pid))
                continue;
            if (IsExcluded(pid, _route))
                continue;
            Route(pid);
        }

        // 清理已退出进程的路由记录
        var stale = new List<uint>();
        foreach (var pid in _routed)
        {
            try { using var _ = Process.GetProcessById((int)pid); }
            catch { stale.Add(pid); }
        }
        foreach (var pid in stale)
        {
            _routed.Remove(pid);
            _attempts.Remove(pid);
        }
    }

    private void ReassertAll()
    {
        if (_route == null)
            return;

        var selfPid = (uint)Process.GetCurrentProcess().Id;
        var pids = new HashSet<uint>(_routed);
        pids.UnionWith(AudioSessionScanner.GetActiveSessionPids());
        foreach (var pid in pids)
        {
            if (pid == selfPid || pid == 0 || pid == 4 || pid == 0xFFFFFFFF)
                continue;
            if (IsExcluded(pid, _route))
                continue;
            Route(pid, force: true);
        }
    }

    private static bool IsExcluded(uint pid, ResolvedRoute route)
    {
        if (route.Exclude.Count == 0)
            return false;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return route.Exclude.Any(pattern => RouterConfig.WildcardMatch(p.ProcessName, pattern));
        }
        catch
        {
            return false;
        }
    }

    private void Route(uint pid, bool force = false)
    {
        var route = _route!;
        var exeName = GetProcessName(pid);
        if (exeName == null)
            return;

        // 手动覆盖按方向独立判断：覆盖了的方向 agent 不碰
        var record = _overrides.Get(exeName);
        var skipRender = record?.Playback != null;
        var skipCapture = record?.Capture != null;
        if (record != null && (skipRender || skipCapture) && _overrideSkippedLogged.Add(exeName))
        {
            var dirs = new List<string>();
            if (skipRender) dirs.Add("播放");
            if (skipCapture) dirs.Add("录音");
            AgentLogger.Log($"跳过手动覆盖: {exeName}（{string.Join("+", dirs)}由用户手动指定）");
        }

        _attempts.TryGetValue(pid, out var attempt);
        var shouldLog = force || attempt == 0 || attempt % 10 == 0;
        var ok = true;

        if (route.PlaybackSwdId != null && !skipRender)
        {
            var hr = _dryRun
                ? HRESULT.S_OK
                : AudioPolicyConfigService.SetPersistedDefaultAudioEndpoint(pid, EDataFlow.eRender, route.PlaybackSwdId);
            if (hr != HRESULT.S_OK)
                ok = false;
            if (shouldLog)
                AgentLogger.Log($"{(force ? "重申" : "路由")}{(_dryRun ? "[dry-run]" : "")} pid={pid} ({exeName}) render → {route.PlaybackDisplay} hr={hr.ToDisplay()}");
        }

        if (route.CaptureSwdId != null && !skipCapture)
        {
            var hr = _dryRun
                ? HRESULT.S_OK
                : AudioPolicyConfigService.SetPersistedDefaultAudioEndpoint(pid, EDataFlow.eCapture, route.CaptureSwdId);
            if (hr != HRESULT.S_OK)
                ok = false;
            if (shouldLog)
                AgentLogger.Log($"{(force ? "重申" : "路由")}{(_dryRun ? "[dry-run]" : "")} pid={pid} ({exeName}) capture → {route.CaptureDisplay} hr={hr.ToDisplay()}");
        }

        if (ok)
        {
            _routed.Add(pid);
            _attempts.Remove(pid);
        }
        else
        {
            _attempts[pid] = attempt + 1;
            // 连续失败 20 次（约 40 秒）后暂停重试，等待 30 秒一轮的全量重申再试
            if (attempt + 1 > 20)
                _routed.Add(pid);
        }
    }

    private static string? GetProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
