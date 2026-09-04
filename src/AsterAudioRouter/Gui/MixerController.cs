// 合成器数据源：每秒轮询本座位音频会话，维护行列表；处理音量/设备/手动覆盖操作。
// 必须在 UI 线程创建（DispatcherTimer）。
using AsterAudioRouter.Interop;
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AsterAudioRouter.Gui;

public sealed class MixerController
{
    public const string AutoChoice = "自动（跟随规则）";
    public const string DefaultChoice = "系统默认";

    public ObservableCollection<SessionRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> DeviceChoices { get; } = new();
    public ObservableCollection<string> CaptureChoices { get; } = new();

    private readonly OverridesStore _overrides;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentUserName { get; private set; } = "";
    public string RuleText { get; private set; } = "";
    public event Action? HeaderChanged;

    public MixerController(OverridesStore overrides)
    {
        _overrides = overrides;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public void Start()
    {
        _ = RefreshDevicesAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var allDevices = await AudioDevices.ListAsync();
            DeviceChoices.Clear();
            DeviceChoices.Add(AutoChoice);
            DeviceChoices.Add(DefaultChoice);
            foreach (var d in allDevices.Where(d => d.IsRender))
                DeviceChoices.Add(d.Name);

            CaptureChoices.Clear();
            CaptureChoices.Add(AutoChoice);
            CaptureChoices.Add(DefaultChoice);
            foreach (var d in allDevices.Where(d => !d.IsRender))
                CaptureChoices.Add(d.Name);
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"设备列表加载失败: {ex.Message}");
        }
    }

    private async Task TickAsync()
    {
        try
        {
            if (CurrentUserName == "")
            {
                CurrentUserName = Environment.UserName;
                try
                {
                    var configPath = RouterConfig.FindConfigPath(AppContext.BaseDirectory);
                    var entries = RouterConfig.Load(configPath);
                    var entry = RouterConfig.Match(entries, CurrentUserName);
                    RuleText = entry == null
                        ? "无匹配规则（agent 空闲）"
                        : $"规则: {entry.User} → 播放 {entry.Playback} / 录音 {entry.Capture}";
                }
                catch
                {
                    RuleText = "配置读取失败";
                }
                HeaderChanged?.Invoke();
            }

            var sessions = AudioSessionScanner.EnumerateSessions();
            var groups = sessions.GroupBy(s => s.Pid).ToList();

            // 移除已退出的应用
            for (var i = Rows.Count - 1; i >= 0; i--)
            {
                if (groups.All(g => g.Key != Rows[i].Pid))
                    Rows.RemoveAt(i);
            }

            foreach (var group in groups)
            {
                var first = group.First();
                var row = Rows.FirstOrDefault(r => r.Pid == group.Key);
                if (row == null)
                {
                    var exeName = GetExeName(group.Key);
                    row = new SessionRowViewModel(group.Key, exeName)
                    {
                        DeviceChoices = DeviceChoices,
                        CaptureChoices = CaptureChoices,
                        DeviceSelection = GetDirectionSelection(exeName, render: true),
                        CaptureSelection = GetDirectionSelection(exeName, render: false)
                    };
                    Rows.Add(row);
                }

                UpdateDisplay(row, first);
                row.VolumeControls.Clear();
                row.VolumeControls.AddRange(group.Select(s => s.Volume));

                // 回读音量/静音（用户拖动时不覆盖）
                if (!row.UserDragging)
                {
                    try
                    {
                        first.Volume.GetMasterVolume(out var vol);
                        row.Volume = vol;
                        row.Muted = first.Volume.GetMute() != 0;
                    }
                    catch
                    {
                        // 会话状态变化中，下一轮再读
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"合成器刷新异常: {ex.Message}");
        }
    }

    private void UpdateDisplay(SessionRowViewModel row, ActiveSessionInfo s)
    {
        try
        {
            var name = s.Session.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(name))
                row.DisplayName = name;
        }
        catch
        {
        }
        if (row.DisplayName == "")
            row.DisplayName = row.ExeName;

        if (row.Icon == null)
        {
            try
            {
                var iconPath = s.Session.GetIconPath();
                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                    row.Icon = GetIcon(iconPath);
            }
            catch
            {
            }
            if (row.Icon == null)
            {
                try
                {
                    using var p = Process.GetProcessById((int)row.Pid);
                    var exePath = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        row.Icon = GetIcon(exePath);
                }
                catch
                {
                }
            }
        }
    }

    private ImageSource? GetIcon(string path)
    {
        if (_iconCache.TryGetValue(path, out var cached))
            return cached;
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null)
                return null;
            using var bmp = icon.ToBitmap();
            var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            _iconCache[path] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string GetExeName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return "未知进程";
        }
    }

    private string GetDirectionSelection(string exeName, bool render)
    {
        var record = _overrides.Get(exeName);
        var value = render ? record?.Playback : record?.Capture;
        if (value == null)
            return AutoChoice;
        var selection = value == "" ? DefaultChoice : value;
        var choices = render ? DeviceChoices : CaptureChoices;
        // 设备名不在下拉列表时（改名/离线），临时加进去以便显示
        if (!choices.Contains(selection))
            choices.Add(selection);
        return selection;
    }

    // ---------- 用户操作 ----------

    /// <summary>恢复自动（按方向）：该方向交还 agent，下轮扫描（≤2s）按规则重新路由。</summary>
    public void SetAuto(string exeName, bool render)
    {
        var record = _overrides.Get(exeName);
        if (record != null)
        {
            _overrides.Set(exeName,
                render ? null : record.Playback,
                render ? record.Capture : null);
        }
        AgentLogger.Log($"恢复自动路由: {exeName}（{(render ? "播放" : "录音")}）");
        SyncSelectionForExe(exeName, AutoChoice, render);
    }

    /// <summary>手动路由（按方向）：立即应用 + 写覆盖记录（同 exe 的全部行同步显示）。</summary>
    public async Task ApplyManualAsync(SessionRowViewModel row, string choice, bool render)
    {
        var value = choice == DefaultChoice ? "" : choice;
        string? swdId = null;
        if (value != "")
        {
            swdId = await AudioDevices.FindSwdIdAsync(value, render);
            if (swdId == null)
            {
                AgentLogger.Log($"手动路由失败，设备未找到: {value}");
                SyncSelectionForExe(row.ExeName, GetDirectionSelection(row.ExeName, render), render);
                return;
            }
        }

        var flow = render ? EDataFlow.eRender : EDataFlow.eCapture;
        var hr = AudioPolicyConfigService.SetPersistedDefaultAudioEndpoint(row.Pid, flow, swdId ?? "");
        AgentLogger.Log($"手动路由 pid={row.Pid} ({row.ExeName}) {(render ? "render" : "capture")} → {(value == "" ? DefaultChoice : value)} hr={hr.ToDisplay()}");

        var existing = _overrides.Get(row.ExeName);
        _overrides.Set(row.ExeName,
            render ? value : existing?.Playback,
            render ? existing?.Capture : value);
        SyncSelectionForExe(row.ExeName, choice, render);
    }

    private void SyncSelectionForExe(string exeName, string selection, bool render)
    {
        foreach (var r in Rows.Where(r => string.Equals(r.ExeName, exeName, StringComparison.OrdinalIgnoreCase)))
        {
            r.UpdatingDeviceSelection = true;
            if (render)
                r.DeviceSelection = selection;
            else
                r.CaptureSelection = selection;
            r.UpdatingDeviceSelection = false;
        }
    }
}
