// SWD 设备 ID 与端点 ID 的转换规则，取自 EarTrumpet DataModel/WindowsAudio/Internal/AudioPolicyConfigService.cs
// <https://github.com/File-New-Project/EarTrumpet> MIT License, Copyright (c) File-New-Project
using System;

namespace AsterAudioRouter;

public static class DeviceId
{
    public const string DevInterfaceAudioRender = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    public const string DevInterfaceAudioCapture = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
    public const string MmdevApiToken = @"\\?\SWD#MMDEVAPI#";

    public static string ToSwdId(string deviceId, bool render)
    {
        if (deviceId.StartsWith(MmdevApiToken, StringComparison.OrdinalIgnoreCase))
            return deviceId;
        return $"{MmdevApiToken}{deviceId}{(render ? DevInterfaceAudioRender : DevInterfaceAudioCapture)}";
    }

    public static string ToEndpointId(string deviceId)
    {
        if (deviceId.StartsWith(MmdevApiToken, StringComparison.OrdinalIgnoreCase))
            deviceId = deviceId.Remove(0, MmdevApiToken.Length);
        if (deviceId.EndsWith(DevInterfaceAudioRender, StringComparison.OrdinalIgnoreCase))
            deviceId = deviceId.Remove(deviceId.Length - DevInterfaceAudioRender.Length);
        else if (deviceId.EndsWith(DevInterfaceAudioCapture, StringComparison.OrdinalIgnoreCase))
            deviceId = deviceId.Remove(deviceId.Length - DevInterfaceAudioCapture.Length);
        return deviceId;
    }
}
