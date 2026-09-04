// 配置读取与用户匹配
//
// config.json 结构（数组，每个元素一条规则）：
// [
//   { "user": "85433", "playback": "Voicemeeter Input", "capture": "" },
//   { "user": "MoMo",  "playback": "Voicemeeter AUX Input", "capture": "" },
//   { "user": "*",     "playback": "", "capture": "" }
// ]
//
// 匹配顺序（用户规则）：先精确匹配，未命中再按配置顺序走 * 通配兜底。
// playback/capture 为空 = 该方向不路由。设备值支持友好名 / SWD ID / 端点 ID。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AsterAudioRouter;

public sealed class RouterEntry
{
    public string User { get; set; } = "";
    public string Playback { get; set; } = "";
    public string Capture { get; set; } = "";
    /// <summary>排除的进程名列表（如 voicemeeter8，防止把混音器自身的输出回路到虚拟输入）。</summary>
    public string[] Exclude { get; set; } = Array.Empty<string>();

    /// <summary>设置窗口用的逗号分隔文本（不参与 JSON 序列化）。</summary>
    [JsonIgnore]
    public string ExcludeText
    {
        get => string.Join(", ", Exclude);
        set => Exclude = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

public sealed class ResolvedRoute
{
    public string? PlaybackSwdId { get; set; }
    public string? CaptureSwdId { get; set; }
    public string PlaybackDisplay { get; set; } = "";
    public string CaptureDisplay { get; set; } = "";
    public HashSet<string> Exclude { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class RouterConfig
{
    public static string ProgramDataPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AsterAudioRouter", "config.json");

    // 配置搜索顺序：exe 同目录（开发/测试，免管理员）→ %ProgramData%（生产，P2 GUI 写入）
    public static string FindConfigPath(string exeDir)
    {
        var local = Path.Combine(exeDir, "config.json");
        return File.Exists(local) ? local : ProgramDataPath;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true // 允许 user/User、playback/Playback 等大小写写法
    };

    /// <summary>保存配置用的选项（设置窗口/右键黑名单共用）。</summary>
    public static readonly JsonSerializerOptions SaveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static List<RouterEntry> Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<RouterEntry>>(json, JsonOptions) ?? new List<RouterEntry>();
    }

    /// <summary>通配匹配（* 任意串），无 * 时即忽略大小写的精确匹配。</summary>
    public static bool WildcardMatch(string text, string pattern)
    {
        if (!pattern.Contains('*'))
            return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
        return Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
    }

    /// <summary>精确匹配优先，然后按配置顺序用 * 通配兜底。</summary>
    public static RouterEntry? Match(List<RouterEntry> entries, string userName)
    {
        var exact = entries.FirstOrDefault(e => string.Equals(e.User, userName, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;
        foreach (var e in entries)
        {
            if (e.User.Contains('*') && WildcardMatch(userName, e.User))
                return e;
        }
        return null;
    }

    /// <summary>把条目中的设备名/ID 解析成 SWD ID；为空则该方向不路由。</summary>
    public static async Task<ResolvedRoute?> ResolveAsync(RouterEntry entry)
    {
        var route = new ResolvedRoute();
        foreach (var name in entry.Exclude)
        {
            if (!string.IsNullOrWhiteSpace(name))
                route.Exclude.Add(name.Trim());
        }
        if (!string.IsNullOrWhiteSpace(entry.Playback))
        {
            var id = await AudioDevices.FindSwdIdAsync(entry.Playback, true);
            if (id != null)
            {
                route.PlaybackSwdId = id;
                route.PlaybackDisplay = entry.Playback;
            }
            else
            {
                AgentLogger.Log($"配置的播放设备未找到: {entry.Playback}");
            }
        }
        if (!string.IsNullOrWhiteSpace(entry.Capture))
        {
            var id = await AudioDevices.FindSwdIdAsync(entry.Capture, false);
            if (id != null)
            {
                route.CaptureSwdId = id;
                route.CaptureDisplay = entry.Capture;
            }
            else
            {
                AgentLogger.Log($"配置的录音设备未找到: {entry.Capture}");
            }
        }
        return route;
    }
}
