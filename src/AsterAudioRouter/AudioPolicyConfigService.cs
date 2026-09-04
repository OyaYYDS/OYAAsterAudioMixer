// 调用模式取自 EarTrumpet DataModel/WindowsAudio/Internal/AudioPolicyConfigService.cs
// <https://github.com/File-New-Project/EarTrumpet> MIT License, Copyright (c) File-New-Project
//
// 要点：
// - 同时设置 eMultimedia + eConsole + eCommunications 三个角色（EarTrumpet 只设前两个；
//   本项目追加通信角色以覆盖"默认通信设备"场景，失败仅记录一次日志不影响整体判定）
// - deviceId 为空 = 恢复默认（传空 HSTRING，清除每应用覆盖）
using AsterAudioRouter.Interop;
using AsterAudioRouter.Interop.Helpers;
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;

namespace AsterAudioRouter;

public static class AudioPolicyConfigService
{
    private static bool _communicationsWarningLogged;

    public static HRESULT SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, string deviceId)
    {
        var factory = AudioPolicyConfigFactory.Create();

        IntPtr hstring = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var str = DeviceId.ToSwdId(deviceId, flow == EDataFlow.eRender);
                Combase.WindowsCreateString(str, (uint)str.Length, out hstring);
            }

            var hrMultimedia = factory.SetPersistedDefaultAudioEndpoint(processId, flow, ERole.eMultimedia, hstring);
            var hrConsole = factory.SetPersistedDefaultAudioEndpoint(processId, flow, ERole.eConsole, hstring);
            var hrCommunications = factory.SetPersistedDefaultAudioEndpoint(processId, flow, ERole.eCommunications, hstring);
            if (hrCommunications != HRESULT.S_OK && !_communicationsWarningLogged)
            {
                _communicationsWarningLogged = true;
                AgentLogger.Log($"eCommunications 角色设置返回 {hrCommunications.ToDisplay()}（本机可能不支持通信角色覆盖，忽略）");
            }
            return hrMultimedia != HRESULT.S_OK ? hrMultimedia : hrConsole;
        }
        finally
        {
            if (hstring != IntPtr.Zero)
                Combase.WindowsDeleteString(hstring);
        }
    }

    public static (HRESULT hr, string? endpointId) GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow)
    {
        var factory = AudioPolicyConfigFactory.Create();
        var hr = factory.GetPersistedDefaultAudioEndpoint(processId, flow, ERole.eMultimedia | ERole.eConsole, out string? deviceId);
        return (hr, deviceId == null ? null : DeviceId.ToEndpointId(deviceId));
    }
}
