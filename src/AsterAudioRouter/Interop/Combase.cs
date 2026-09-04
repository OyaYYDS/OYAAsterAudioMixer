// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
// .NET 8 适配（手动 HSTRING，原因见 src/AsterAudioP0 同文件注释与 knowledge/windows-per-app-audio-routing.md）
using System;
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop;

internal static class Combase
{
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

    [DllImport("combase.dll")]
    public static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
}
