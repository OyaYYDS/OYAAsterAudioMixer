// 手动覆盖存储：%AppData%\AsterAudioRouter\overrides.json（每用户独立，两座位互不影响）
//
// 语义（按方向独立）：
//   null    = 自动（跟随 config.json 用户规则，agent 正常路由该方向）
//   ""      = 手动 + 系统默认（agent 跳过该方向；设备已由调用方清空为默认）
//   "设备名" = 手动 + 指定设备（agent 跳过该方向）
// 两个方向都 null 的记录会被自动删除。
// 单实例架构下 GUI 与 agent 同进程，直接共享实例；无需跨进程监听。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsterAudioRouter;

public sealed class OverrideRecord
{
    public string Exe { get; set; } = "";
    public string? Playback { get; set; }
    public string? Capture { get; set; }

    [JsonIgnore]
    public string PlaybackDisplay => Playback == null ? "自动" : (Playback == "" ? "系统默认" : Playback);

    [JsonIgnore]
    public string CaptureDisplay => Capture == null ? "自动" : (Capture == "" ? "系统默认" : Capture);
}

public sealed class OverridesStore
{
    private sealed class OverridesDoc
    {
        public List<OverrideRecord> Overrides { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly string _path;
    private List<OverrideRecord> _records = new();

    public string Path { get; }

    public OverridesStore()
    {
        Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AsterAudioRouter", "overrides.json");
        Load();
    }

    public event Action? Changed;

    private void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(Path))
                {
                    var doc = JsonSerializer.Deserialize<OverridesDoc>(File.ReadAllText(Path), JsonOptions);
                    _records = doc?.Overrides ?? new List<OverrideRecord>();
                }
                else
                {
                    _records = new List<OverrideRecord>();
                }
            }
            catch (Exception ex)
            {
                AgentLogger.Log($"覆盖文件读取失败: {ex.Message}");
                _records = new List<OverrideRecord>();
            }
        }
    }

    public OverrideRecord? Get(string exeName)
    {
        lock (_lock)
        {
            return _records.FirstOrDefault(r => string.Equals(r.Exe, exeName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<OverrideRecord> All
    {
        get { lock (_lock) return _records.ToList(); }
    }

    /// <summary>更新某 exe 的覆盖状态；两个方向都为 null 时删除记录。</summary>
    public void Set(string exeName, string? playback, string? capture)
    {
        lock (_lock)
        {
            var record = _records.FirstOrDefault(r => string.Equals(r.Exe, exeName, StringComparison.OrdinalIgnoreCase));
            if (record == null)
            {
                record = new OverrideRecord { Exe = exeName };
                _records.Add(record);
            }
            record.Playback = playback;
            record.Capture = capture;
            if (playback == null && capture == null)
            {
                _records.Remove(record);
            }
            Save();
        }
        Changed?.Invoke();
    }

    public void Remove(string exeName)
    {
        lock (_lock)
        {
            _records.RemoveAll(r => string.Equals(r.Exe, exeName, StringComparison.OrdinalIgnoreCase));
            Save();
        }
        Changed?.Invoke();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new OverridesDoc { Overrides = _records }, JsonOptions));
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"覆盖文件保存失败: {ex.Message}");
        }
    }
}
