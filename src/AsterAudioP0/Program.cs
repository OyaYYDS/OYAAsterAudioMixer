// AsterAudioP0 — P0 尖刺工具：验证"双用户同开同一程序互不串扰"的核心假设
//
// 用法:
//   双击直接运行（无参数）→ 进入交互式编号菜单，按数字操作，全程中文提示
//   命令行模式:
//     AsterAudioP0.exe list-devices
//     AsterAudioP0.exe get <pid> render|capture
//     AsterAudioP0.exe set <pid> render|capture <设备名|SWD ID|端点 ID>
//     AsterAudioP0.exe find <进程名>
//     AsterAudioP0.exe whoami
//
// 日志目录: 环境变量 ASTER_P0_LOGS 优先，否则 <exe目录>\p0logs
using AsterAudioP0.Interop;
using AsterAudioP0.Interop.MMDeviceAPI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AsterAudioP0;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint codePageId);

    private static string LogDir
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ASTER_P0_LOGS");
            if (!string.IsNullOrWhiteSpace(env))
                return env;
            return Path.Combine(AppContext.BaseDirectory, "p0logs");
        }
    }

    private static async Task<int> Main(string[] args)
    {
        SetConsoleOutputCP(65001);
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Logger.Init(LogDir);
        Logger.Log($"启动: {(args.Length == 0 ? "(交互模式)" : string.Join(' ', args))}");

        try
        {
            if (args.Length == 0)
                return await InteractiveLoopAsync();
            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            Logger.Log($"异常: {ex}");
            if (args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            return 1;
        }
    }

    // ---------- 命令行模式 ----------

    private static async Task<int> RunAsync(string[] args)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "list-devices":
                await ListDevicesAsync();
                return 0;

            case "whoami":
                WhoAmI();
                return 0;

            case "find":
                if (args.Length != 2)
                {
                    PrintUsage();
                    return 2;
                }
                FindProcesses(args[1]);
                return 0;

            case "get":
                if (args.Length != 3 || !TryParseFlow(args[2], out var flowGet) || !TryParsePid(args[1], out var pidGet))
                {
                    PrintUsage();
                    return 2;
                }
                GetPersisted(pidGet, flowGet);
                return 0;

            case "set":
                if (args.Length != 4 || !TryParseFlow(args[2], out var flowSet) || !TryParsePid(args[1], out var pidSet))
                {
                    PrintUsage();
                    return 2;
                }
                await SetPersistedAsync(pidSet, flowSet, args[3]);
                return 0;

            default:
                PrintUsage();
                return 2;
        }
    }

    // ---------- 交互模式（双击直接运行） ----------

    private static async Task<int> InteractiveLoopAsync()
    {
        var currentSession = Process.GetCurrentProcess().SessionId;
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("   ASTER P0 音频路由测试工具");
        Console.WriteLine($"   当前用户: {Environment.UserName}    会话: {currentSession}");
        Console.WriteLine("   所有操作自动记录到本目录 p0logs\\ 文件夹");
        Console.WriteLine("==============================================");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("   1. 查看当前用户信息");
            Console.WriteLine("   2. 列出所有音频设备");
            Console.WriteLine("   3. 查找进程 PID");
            Console.WriteLine("   4. 设置进程的扬声器/麦克风");
            Console.WriteLine("   5. 查询进程的扬声器/麦克风");
            Console.WriteLine("   0. 退出");
            Console.Write("   请选择: ");
            var line = Console.ReadLine();
            if (line == null)
                return 0;

            switch (line.Trim())
            {
                case "0":
                    return 0;
                case "1":
                    WhoAmI();
                    break;
                case "2":
                    await ListDevicesAsync();
                    break;
                case "3":
                    InteractiveFind();
                    break;
                case "4":
                    await InteractiveSetAsync();
                    break;
                case "5":
                    InteractiveGet();
                    break;
                default:
                    Console.WriteLine("   无效选择，请输入 0-5");
                    break;
            }
        }
    }

    private static void InteractiveFind()
    {
        var name = Prompt("   进程名(如 chrome): ");
        if (name == null)
            return;
        FindProcesses(name);
    }

    private static async Task InteractiveSetAsync()
    {
        var pid = PickProcess();
        if (pid == null)
            return;

        var flow = PickFlow();
        if (flow == null)
            return;

        var device = await PickDeviceAsync(flow.Value);
        if (device == null)
            return;

        await SetPersistedAsync(pid.Value, flow.Value, device);
    }

    private static void InteractiveGet()
    {
        var pid = PickProcess();
        if (pid == null)
            return;

        var flow = PickFlow();
        if (flow == null)
            return;

        GetPersisted(pid.Value, flow.Value);
    }

    private static uint? PickProcess()
    {
        var name = Prompt("   输入进程名(如 chrome): ");
        if (string.IsNullOrEmpty(name))
            return null;

        var currentSession = Process.GetCurrentProcess().SessionId;
        var procs = Process.GetProcessesByName(name)
            .Where(p => p.SessionId == currentSession)
            .ToList();

        if (procs.Count == 0)
        {
            Console.WriteLine($"   本座位没有正在运行的进程: {name}");
            Console.WriteLine("   （可能只在另一个座位运行，不能动别人的进程）");
            return null;
        }

        Console.WriteLine("   0. 取消");
        for (var i = 0; i < procs.Count; i++)
            Console.WriteLine($"   {i + 1}. pid={procs[i].Id}");
        if (procs.Count > 1)
            Console.WriteLine("   提示: 设置按\"程序\"生效而非单个进程，任选一个即可（建议选 1）");
        var choice = ReadNumber(procs.Count);
        if (choice == null || choice == 0)
            return null;
        return (uint)procs[choice.Value - 1].Id;
    }

    private static EDataFlow? PickFlow()
    {
        Console.WriteLine("   方向: 1=扬声器(render)  2=麦克风(capture)");
        var choice = ReadNumber(2);
        return choice switch
        {
            null => null,
            0 => null,
            1 => EDataFlow.eRender,
            _ => EDataFlow.eCapture,
        };
    }

    private static async Task<string?> PickDeviceAsync(EDataFlow flow)
    {
        var devices = (await AudioDevices.ListAsync())
            .Where(d => d.IsRender == (flow == EDataFlow.eRender))
            .ToList();
        if (devices.Count == 0)
        {
            Console.WriteLine("   没有找到设备");
            return null;
        }

        Console.WriteLine("   0. 取消");
        for (var i = 0; i < devices.Count; i++)
            Console.WriteLine($"   {i + 1}. {devices[i].Name}{(AudioDevices.IsDefault(devices[i]) ? "  【本座位当前默认】" : "")}");
        var choice = ReadNumber(devices.Count);
        if (choice == null || choice == 0)
            return null;
        return devices[choice.Value - 1].Name;
    }

    private static string? Prompt(string text)
    {
        Console.Write(text);
        return Console.ReadLine()?.Trim();
    }

    private static int? ReadNumber(int max)
    {
        Console.Write("   请输入编号: ");
        var line = Console.ReadLine();
        if (!int.TryParse(line, out var n) || n < 0 || n > max)
        {
            Console.WriteLine("   输入无效");
            return null;
        }
        return n;
    }

    // ---------- 命令实现 ----------

    private static async Task ListDevicesAsync()
    {
        var devices = await AudioDevices.ListAsync();
        foreach (var d in devices)
        {
            var tag = d.IsRender ? "render" : "capture";
            Logger.Log($"[{tag}] {d.Name}{(AudioDevices.IsDefault(d) ? "  【本座位当前默认】" : "")}");
            Logger.Log($"       SWD ID = {d.Id}");
            Logger.Log($"       端点 ID = {DeviceId.ToEndpointId(d.Id)}");
        }
        if (devices.Count == 0)
            Logger.Log("未枚举到任何音频设备");
    }

    private static void WhoAmI()
    {
        Logger.Log($"whoami: user={Environment.UserName} domain={Environment.UserDomainName} session={Process.GetCurrentProcess().SessionId} osBuild={Environment.OSVersion.Version.Build}");
    }

    private static void GetPersisted(uint pid, EDataFlow flow)
    {
        Logger.Log($"get pid={pid} ({DescribeProcess(pid)}) flow={flow}");
        var (hr, endpointId) = AudioPolicyConfigService.GetPersistedDefaultAudioEndpoint(pid, flow);
        Logger.Log($"GetPersistedDefaultAudioEndpoint 结果: {hr.ToDisplay()}, endpointId={endpointId ?? "(null)"}");
    }

    private static async Task SetPersistedAsync(uint pid, EDataFlow flow, string nameOrId)
    {
        Logger.Log($"set pid={pid} ({DescribeProcess(pid)}) flow={flow} target={nameOrId}");

        string? swdId;
        if (nameOrId.StartsWith(DeviceId.MmdevApiToken, StringComparison.OrdinalIgnoreCase) ||
            nameOrId.StartsWith("{")) // 裸端点 ID 形式 {0.0.0.00000000}.{guid}，Service 会自动包装
        {
            swdId = nameOrId;
        }
        else
        {
            swdId = await AudioDevices.FindSwdIdAsync(nameOrId, flow == EDataFlow.eRender);
            if (swdId == null)
            {
                Logger.Log($"未找到设备: {nameOrId}");
                return;
            }
            Logger.Log($"设备解析: {nameOrId} -> {swdId}");
        }

        var hr = AudioPolicyConfigService.SetPersistedDefaultAudioEndpoint(pid, flow, swdId);
        Logger.Log($"SetPersistedDefaultAudioEndpoint 结果: {hr.ToDisplay()}");
        Console.WriteLine(hr == HRESULT.S_OK
            ? "   设置成功，请用耳朵确认声音方向"
            : $"   设置返回: {hr.ToDisplay()}（程序可能还没有音频流，稍后再设一次试试）");
    }

    private static void FindProcesses(string name)
    {
        var matches = Process.GetProcessesByName(name);
        if (matches.Length == 0)
        {
            Logger.Log($"未找到进程: {name}");
            return;
        }
        foreach (var p in matches)
        {
            try
            {
                Logger.Log($"pid={p.Id} name={p.ProcessName} session={p.SessionId} path={p.MainModule?.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Log($"pid={p.Id} name={p.ProcessName} session={p.SessionId} (path不可读: {ex.Message})");
            }
        }
    }

    private static string DescribeProcess(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return $"name={p.ProcessName} session={p.SessionId}";
        }
        catch (Exception ex)
        {
            return $"无法访问进程信息({ex.Message})";
        }
    }

    private static bool TryParsePid(string s, out uint pid)
    {
        if (uint.TryParse(s, out pid))
            return true;
        Logger.Log($"无效 PID: {s}");
        return false;
    }

    private static bool TryParseFlow(string s, out EDataFlow flow)
    {
        switch (s.ToLowerInvariant())
        {
            case "render":
                flow = EDataFlow.eRender;
                return true;
            case "capture":
                flow = EDataFlow.eCapture;
                return true;
            default:
                Logger.Log($"无效 flow: {s}（应为 render 或 capture）");
                flow = EDataFlow.eRender;
                return false;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            AsterAudioP0 — P0 尖刺工具（ASTER 双用户同程序实测）

            用法:
              AsterAudioP0.exe list-devices
              AsterAudioP0.exe get <pid> render|capture
              AsterAudioP0.exe set <pid> render|capture <设备名|SWD ID|端点 ID>
              AsterAudioP0.exe find <进程名>
              AsterAudioP0.exe whoami

            不带参数运行 = 进入交互式菜单
            """);
    }
}
