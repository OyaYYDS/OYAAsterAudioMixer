// 设备枚举：Windows.Devices.Enumeration（公开 WinRT API）
// DeviceInformation.Id 即 SWD 格式设备 ID，可直接传给 SetPersistedDefaultAudioEndpoint（见 DeviceId.cs）
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace AsterAudioRouter;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsRender);

public static class AudioDevices
{
    public static async Task<List<AudioDeviceInfo>> ListAsync()
    {
        var list = new List<AudioDeviceInfo>();
        var renderDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
        foreach (var d in renderDevices)
            list.Add(new AudioDeviceInfo(d.Id, d.Name, true));
        var captureDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
        foreach (var d in captureDevices)
            list.Add(new AudioDeviceInfo(d.Id, d.Name, false));
        return list;
    }

    /// <summary>按友好名 / SWD ID / 端点 ID 查找设备，返回 SWD ID。友好名支持前缀匹配（可写简称）。</summary>
    public static async Task<string?> FindSwdIdAsync(string nameOrId, bool render)
    {
        var devices = await DeviceInformation.FindAllAsync(render ? DeviceClass.AudioRender : DeviceClass.AudioCapture);

        // 第一轮：精确匹配（友好名 / SWD ID / 端点 ID）
        foreach (var d in devices)
        {
            if (string.Equals(d.Id, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(DeviceId.ToEndpointId(d.Id), nameOrId, StringComparison.OrdinalIgnoreCase))
            {
                return d.Id;
            }
        }
        // 第二轮：友好名前缀匹配（如 "Voicemeeter Input" 命中 "Voicemeeter Input (VB-Audio ...)"）
        foreach (var d in devices)
        {
            if (d.Name.StartsWith(nameOrId, StringComparison.OrdinalIgnoreCase))
                return d.Id;
        }
        return null;
    }
}
