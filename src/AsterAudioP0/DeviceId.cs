// SWD 设备 ID 与端点 ID 的转换规则，取自 EarTrumpet DataModel/WindowsAudio/Internal/AudioPolicyConfigService.cs
// <https://github.com/File-New-Project/EarTrumpet> MIT License, Copyright (c) File-New-Project
//
// SetPersistedDefaultAudioEndpoint 期望的是 SWD 格式设备 ID：
//   \\?\SWD#MMDEVAPI#{端点ID}#{接口类GUID}
// 其中接口类 GUID：render = e6327cad-dcec-4949-ae8a-991e976a79d2，capture = 2eef81be-33fa-4800-9670-1cd474972c3f
using System;

namespace AsterAudioP0;

public static class DeviceId
{
    public const string DevInterfaceAudioRender = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    public const string DevInterfaceAudioCapture = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
    public const string MmdevApiToken = @"\\?\SWD#MMDEVAPI#";

    /// <summary>把任意形式（SWD ID / 端点 ID / 友好名已由调用方解析为 SWD ID）统一成 SWD ID。</summary>
    public static string ToSwdId(string deviceId, bool render)
    {
        if (deviceId.StartsWith(MmdevApiToken, StringComparison.OrdinalIgnoreCase))
            return deviceId;
        return $"{MmdevApiToken}{deviceId}{(render ? DevInterfaceAudioRender : DevInterfaceAudioCapture)}";
    }

    /// <summary>把 SWD ID 还原为裸端点 ID（{0.0.0.00000000}.{guid}）。</summary>
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
