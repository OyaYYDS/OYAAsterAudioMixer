// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
// IPropertyStore 为占位声明（见 Interop/OpaqueTypes.cs），方法顺序保持 vtable 一致
using AsterAudioRouter.Interop;
using System;
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice
{
    void Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.Interface)] out object ppInterface);
    [return: MarshalAs(UnmanagedType.Interface)]
    IPropertyStore OpenPropertyStore(uint stgmAccess);
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetId();
    DeviceState GetState();
}

public static class IMMDeviceExtensions
{
    public static T Activate<T>(this IMMDevice device)
    {
        Guid iid = typeof(T).GUID;
        device.Activate(ref iid, 0x1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out object ret);
        return (T)ret;
    }
}
