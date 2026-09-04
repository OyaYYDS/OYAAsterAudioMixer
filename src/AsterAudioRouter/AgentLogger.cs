// 日志：控制台（可选）+ 追加写 p1logs\agent-<日期>.log
using System;
using System.Diagnostics;
using System.IO;

namespace AsterAudioRouter;

public static class AgentLogger
{
    private static string? _logFile;
    private static bool _consoleEnabled = true;

    public static void Init(string logDir, bool consoleEnabled)
    {
        _consoleEnabled = consoleEnabled;
        Directory.CreateDirectory(logDir);
        _logFile = Path.Combine(logDir, $"agent-{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [user={Environment.UserName}] [session={Process.GetCurrentProcess().SessionId}] {message}";
        if (_consoleEnabled)
            Console.WriteLine(line);
        try
        {
            if (_logFile != null)
                File.AppendAllText(_logFile, line + Environment.NewLine);
        }
        catch
        {
            // 日志写入失败不影响运行
        }
    }
}
