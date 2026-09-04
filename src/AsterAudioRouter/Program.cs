// AsterAudioRouter — ASTER 按用户音频自动路由 + 音量合成器（P2）
//
// 模式:
//   无参数      = GUI：托盘 + 合成器窗口（内置 agent 循环），控制台自动隐藏
//   --agent     = 无窗口后台 agent（每座位登录自启）
//   --dry-run   = 试运行：只记录将执行的路由
//   --status / --install / --uninstall / --reload = CLI 子命令
//
// 单实例：长驻模式（GUI/--agent）按会话互斥，每座位只一个实例；CLI 一次性命令不受限。
// 配置: exe 同目录 config.json（优先）或 %ProgramData%\AsterAudioRouter\config.json
// 手动覆盖: %AppData%\AsterAudioRouter\overrides.json（每用户独立）
// 日志: exe 同目录 p1logs\
using AsterAudioRouter.Gui;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AsterAudioRouter;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint codePageId);

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    // 注意：不能使用 async Main —— .NET 的 async Main 不认 [STAThread]（线程池执行导致
    // WPF 报 "The calling thread must be STA"）。Main 保持同步，agent 模式内部阻塞等待。
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var arg = args.Length == 0 ? "" : args[0].ToLowerInvariant();
        var isCliMode = arg is "--status" or "--install" or "--uninstall" or "--reload" or "--dry-run";

        // WinExe 子系统默认无控制台（GUI/--agent 双击零黑窗）；
        // CLI 模式先挂接父进程控制台（cmd 里运行），挂不上（资源管理器启动）再自建一个
        if (isCliMode)
        {
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();
            try { SetConsoleOutputCP(65001); } catch { }
        }

        var exeDir = AppContext.BaseDirectory;
        AgentLogger.Init(Path.Combine(exeDir, "p1logs"), consoleEnabled: true);

        // 长驻模式（GUI / --agent）：按会话单实例互斥
        if (arg is "" or "--agent")
        {
            var mutexName = $"AsterAudioRouter_S{Process.GetCurrentProcess().SessionId}";
            using var mutex = new Mutex(true, mutexName, out var createdNew);
            if (!createdNew)
            {
                if (arg == "")
                    System.Windows.Forms.MessageBox.Show(
                        "本座位已有 ASTER 音量合成器在运行。\n（若在后台运行，可到托盘或任务管理器操作）",
                        "ASTER 音量合成器");
                return 0;
            }

            if (arg == "--agent")
            {
                return RunAgentModeAsync(exeDir, dryRun: false).GetAwaiter().GetResult();
            }
            return RunGuiMode(exeDir);
        }

        switch (arg)
        {
            case "--dry-run":
                AgentLogger.Log("dry-run 模式：只记录将要执行的路由，不真正调用接口。Ctrl+C 退出");
                return RunAgentModeAsync(exeDir, dryRun: true).GetAwaiter().GetResult();
            case "--status":
                PrintStatus(exeDir);
                return 0;
            case "--install":
                Autostart.Install();
                return 0;
            case "--uninstall":
                Autostart.Uninstall();
                return 0;
            case "--reload":
                try
                {
                    using var ev = EventWaitHandle.OpenExisting("AsterAudioRouterReload");
                    ev.Set();
                    Console.WriteLine("已通知 agent 重新加载配置");
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Console.WriteLine("没有正在运行的 agent");
                }
                return 0;
            default:
                PrintUsage();
                return 0;
        }
    }

    private static async Task<int> RunAgentModeAsync(string exeDir, bool dryRun)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var overrides = new OverridesStore();
        using var agent = new Agent(exeDir, dryRun, overrides);
        await agent.RunAsync(cts.Token);
        return 0;
    }

    // 注意：必须在 STA/UI 线程上完整执行（WPF 对象与 DispatcherTimer），内部不用 await
    private static int RunGuiMode(string exeDir)
    {
        var overrides = new OverridesStore();
        using var cts = new CancellationTokenSource();
        using var agent = new Agent(exeDir, dryRun: false, overrides);
        var agentTask = agent.RunAsync(cts.Token);

        var controller = new MixerController(overrides);
        var window = new MainWindow(controller, overrides);
        var iconFile = Path.Combine(exeDir, "icon.ico");
        if (File.Exists(iconFile))
        {
            try { window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconFile)); }
            catch { /* 图标加载失败不影响运行 */ }
        }
        using var tray = new TrayIcon(
            openMixer: () => { window.Show(); window.Activate(); },
            openSettings: () =>
            {
                window.Show();
                var settings = new SettingsWindow(overrides) { Owner = window };
                settings.ShowDialog();
            },
            exit: () =>
            {
                cts.Cancel();
                System.Windows.Application.Current.Shutdown();
            });

        var app = new App();
        app.InitializeComponent();
        controller.Start();
        window.Show();
        app.Run();

        controller.Stop();
        cts.Cancel();
        try { agentTask.Wait(3000); } catch { /* 取消时的任务异常忽略 */ }
        return 0;
    }

    private static void PrintStatus(string exeDir)
    {
        var configPath = RouterConfig.FindConfigPath(exeDir);
        Console.WriteLine($"配置路径: {configPath}（存在: {File.Exists(configPath)}）");
        Console.WriteLine($"当前用户: {Environment.UserName}（session {Process.GetCurrentProcess().SessionId}）");
        try
        {
            var entries = RouterConfig.Load(configPath);
            var entry = RouterConfig.Match(entries, Environment.UserName);
            Console.WriteLine(entry == null
                ? "匹配规则: 无（agent 将空闲）"
                : $"匹配规则: user={entry.User}  playback={entry.Playback}  capture={entry.Capture}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"配置读取失败: {ex.Message}");
        }

        var pids = AudioSessionScanner.GetActiveSessionPids();
        Console.WriteLine($"当前有音频会话的进程数: {pids.Count}");
        foreach (var pid in pids)
        {
            var name = "?";
            try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
            catch { }
            Console.WriteLine($"  pid={pid}  {name}");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            AsterAudioRouter — ASTER 按用户音频自动路由 + 音量合成器（P2）

            用法:
              （无参数）              = 打开音量合成器（托盘 + 窗口，内置自动路由）
              AsterAudioRouter.exe --agent      后台运行（每座位登录自启）
              AsterAudioRouter.exe --dry-run    试运行：只记录将执行的路由，不真正调用
              AsterAudioRouter.exe --status     查看配置/匹配规则/有音频会话的进程
              AsterAudioRouter.exe --install    写入当前用户登录自启（HKCU Run）
              AsterAudioRouter.exe --uninstall  移除当前用户登录自启
              AsterAudioRouter.exe --reload     通知运行中的 agent 重新加载配置

            配置: exe 同目录 config.json（优先）或 %ProgramData%\AsterAudioRouter\config.json
            手动覆盖: %AppData%\AsterAudioRouter\overrides.json
            日志: exe 同目录 p1logs\
            """);
    }
}
