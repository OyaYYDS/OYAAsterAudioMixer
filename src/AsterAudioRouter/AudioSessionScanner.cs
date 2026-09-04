// 音频会话扫描：枚举本会话所有活动端点的音频会话。
// 每座位 agent/GUI 只看到本会话的设备与进程，天然按用户隔离。
// 这也实现了用户规则"仅在进程存在对应音频 Session 时执行应用级音频路由"。
using AsterAudioRouter.Interop;
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AsterAudioRouter;

public sealed class ActiveSessionInfo
{
    public uint Pid { get; init; }
    public IAudioSessionControl Session { get; init; } = null!;    // 显示名/图标
    public IAudioSessionControl2 Session2 { get; init; } = null!; // 进程信息
    public ISimpleAudioVolume Volume { get; init; } = null!;      // 音量/静音
}

public static class AudioSessionScanner
{
    /// <summary>返回本会话所有有音频会话的进程 PID（去重）。</summary>
    public static HashSet<uint> GetActiveSessionPids()
        => EnumerateSessions().Select(s => s.Pid).ToHashSet();

    /// <summary>枚举本会话全部活动端点上的音频会话（按 pid 去重，保留首个）。</summary>
    public static List<ActiveSessionInfo> EnumerateSessions()
    {
        var result = new List<ActiveSessionInfo>();
        var selfPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

            // 先枚举 render 再枚举 capture：同一 pid 同时有播放会话和录音会话时
            // （QQ/steam 等），去重后保留播放会话——音量/静音操作要作用在播放会话上才有效。
            foreach (var flow in new[] { EDataFlow.eRender, EDataFlow.eCapture })
            {
                IMMDeviceCollection devices;
                try { devices = enumerator.EnumAudioEndpoints(flow, DeviceState.ACTIVE); }
                catch { continue; }

                var deviceCount = devices.GetCount();
                for (uint i = 0; i < deviceCount; i++)
                {
                    IMMDevice device;
                    try { device = devices.Item(i); }
                    catch { continue; }

                    try
                    {
                        var manager = device.Activate<IAudioSessionManager2>();
                        var sessions = manager.GetSessionEnumerator();
                        var sessionCount = sessions.GetCount();
                        for (var j = 0; j < sessionCount; j++)
                        {
                            try
                            {
                                var session = sessions.GetSession(j);
                                var session2 = (IAudioSessionControl2)session;
                                // GetProcessId 为 PreserveSig：返回 0(S_OK) 时 pid 有效；
                                // 0xFFFFFFFF 为系统音效会话，跳过
                                if (session2.GetProcessId(out var pid) != 0 || pid == 0 || pid == 0xFFFFFFFF)
                                    continue;
                                if (pid == selfPid)
                                    continue;

                                // 注意：不能用 manager.GetSimpleAudioVolume(Guid.Empty) —— 那是"本进程"
                                // 的会话音量，还会顺带为本进程创建音频会话（列表里出现自己、各行音量联动）。
                                // 正确姿势：对会话对象 QI ISimpleAudioVolume（每会话独立）。
                                var volume = (ISimpleAudioVolume)session;
                                result.Add(new ActiveSessionInfo
                                {
                                    Pid = pid,
                                    Session = session,
                                    Session2 = session2,
                                    Volume = volume
                                });
                            }
                            catch
                            {
                                // 个别会话读取失败不影响整体
                            }
                        }
                    }
                    catch
                    {
                        // 个别端点无法激活会话管理器（如无会话的设备），忽略
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"会话枚举异常: {ex.Message}");
        }
        return result;
    }
}
