// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System;
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionEvents
{
    void OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string NewDisplayName, ref Guid EventContext);
    void OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string NewIconPath, ref Guid EventContext);
    void OnSimpleVolumeChanged(float NewVolume, int NewMute, ref Guid EventContext);
    void OnChannelVolumeChanged(uint ChannelCount, IntPtr afNewChannelVolume, uint ChangedChannel, ref Guid EventContext);
    void OnGroupingParamChanged(ref Guid NewGroupingParam, ref Guid EventContext);
    void OnStateChanged(AudioSessionState NewState);
    void OnSessionDisconnected(AudioSessionDisconnectReason DisconnectReason);
}
