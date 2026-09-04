// 调用模式取自 EarTrumpet DataModel/WindowsAudio/Internal/AudioPolicyConfigService.cs
// <https://github.com/File-New-Project/EarTrumpet> MIT License, Copyright (c) File-New-Project
//
// 要点：
// - render 方向同时设置 eMultimedia + eConsole 两个角色（EarTrumpet 行为，与设置页效果一致）
// - deviceId 为空 = 恢复默认（传空 HSTRING）
using AsterAudioP0.Interop;
using AsterAudioP0.Interop.Helpers;
using AsterAudioP0.Interop.MMDeviceAPI;
using System;

namespace AsterAudioP0;

public static class AudioPolicyConfigService
{
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
