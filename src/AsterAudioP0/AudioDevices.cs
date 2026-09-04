// 设备枚举：Windows.Devices.Enumeration（公开 WinRT API）
// DeviceInformation.Id 即 SWD 格式设备 ID，可直接传给 SetPersistedDefaultAudioEndpoint（见 DeviceId.cs）
// 本座位默认设备：Windows.Media.Devices.MediaDevice（公开 WinRT API），只反映当前会话的默认
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace AsterAudioP0;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsRender);

public static class AudioDevices
{
    public static string DefaultRenderId => MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
    public static string DefaultCaptureId => MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);

    /// <summary>该设备是否是本会话（本座位）当前默认设备。</summary>
    public static bool IsDefault(AudioDeviceInfo d)
    {
        var target = d.IsRender ? DefaultRenderId : DefaultCaptureId;
        return string.Equals(d.Id, target, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(DeviceId.ToEndpointId(d.Id), target, StringComparison.OrdinalIgnoreCase);
    }
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

    /// <summary>按友好名 / SWD ID / 端点 ID 查找设备，返回 SWD ID。</summary>
    public static async Task<string?> FindSwdIdAsync(string nameOrId, bool render)
    {
        var devices = await DeviceInformation.FindAllAsync(render ? DeviceClass.AudioRender : DeviceClass.AudioCapture);
        foreach (var d in devices)
        {
            if (string.Equals(d.Id, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(DeviceId.ToEndpointId(d.Id), nameOrId, StringComparison.OrdinalIgnoreCase))
            {
                return d.Id;
            }
        }
        return null;
    }
}
