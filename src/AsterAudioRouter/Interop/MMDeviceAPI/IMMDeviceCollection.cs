// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection
{
    uint GetCount();
    [return: MarshalAs(UnmanagedType.Interface)]
    IMMDevice Item(uint nDevice);
}
