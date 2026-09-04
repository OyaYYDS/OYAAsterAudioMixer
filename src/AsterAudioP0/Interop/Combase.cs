// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
//
// 与 EarTrumpet 原版差异：EarTrumpet 面向 .NET Framework，其 DllImport 参数用
// [MarshalAs(UnmanagedType.HString)]；.NET 8 的 P/Invoke 封送器不支持 HString
// （运行时抛 MarshalDirectiveException），故激活路径改为手动 HSTRING 指针
// （WindowsCreateString/WindowsDeleteString + 裸指针 + Marshal.GetObjectForIUnknown），
// 逻辑等价。接口方法（IInspectable）上的 HString 封送由 .NET 8 内置 WinRT 互操作层处理。
using System;
using System.Runtime.InteropServices;

namespace AsterAudioP0.Interop;

internal static class Combase
{
    // HRESULT RoGetActivationFactory(HSTRING activatableClassId, REFIID iid, void **factory)
    [DllImport("combase.dll", PreserveSig = true)]
    public static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        [In] ref Guid iid,
        [Out] out IntPtr factory);

    [DllImport("combase.dll", PreserveSig = false)]
    public static extern void WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string src,
        [In] uint length,
        [Out] out IntPtr hstring);

    [DllImport("combase.dll")]
    public static extern int WindowsDeleteString(IntPtr hstring);

    // PCWSTR WindowsGetStringRawBuffer(HSTRING string, UINT32 *length)
    [DllImport("combase.dll")]
    public static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
}
