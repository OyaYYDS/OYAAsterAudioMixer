// 日志：控制台 + 追加写 p0logs\p0-<日期>.log（两座位结果汇总同一目录）
using System;
using System.Diagnostics;
using System.IO;

namespace AsterAudioP0;

public static class Logger
{
    private static string? _logFile;

    public static void Init(string logDir)
    {
        Directory.CreateDirectory(logDir);
        _logFile = Path.Combine(logDir, $"p0-{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [user={Environment.UserName}] [session={Process.GetCurrentProcess().SessionId}] {message}";
        Console.WriteLine(line);
        if (_logFile != null)
            File.AppendAllText(_logFile, line + Environment.NewLine);
    }
}
