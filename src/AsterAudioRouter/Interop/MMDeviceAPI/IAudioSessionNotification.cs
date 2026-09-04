// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

[Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionNotification
{
    void OnSessionCreated([MarshalAs(UnmanagedType.Interface)] IAudioSessionControl NewSession);
}
