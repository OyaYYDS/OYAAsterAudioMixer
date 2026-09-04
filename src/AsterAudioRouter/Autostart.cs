// 当前用户登录自启（HKCU\Software\Microsoft\Windows\CurrentVersion\Run）
// 每个座位各自执行一次 --install（各自写各自的 HKCU）
using Microsoft.Win32;
using System.Diagnostics;

namespace AsterAudioRouter;

public static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AsterAudioRouter";

    public static void Install()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exe))
        {
            AgentLogger.Log("无法取得本程序路径，自启写入失败");
            return;
        }
        var command = $"\"{exe}\" --agent";
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, command);
        AgentLogger.Log($"已写入当前用户登录自启: {command}");
    }

    public static bool IsInstalled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Uninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) == null)
        {
            AgentLogger.Log("当前用户没有自启项，无需移除");
            return;
        }
        key.DeleteValue(ValueName);
        AgentLogger.Log("已移除当前用户登录自启");
    }
}
