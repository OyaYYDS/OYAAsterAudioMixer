// 裸指针 + vtable 直调方案（思路与 SoundSwitch AudioPolicyConfig.cs 一致，
// <https://github.com/Belphemur/SoundSwitch> GPLv3 仅作事实对照，本实现为独立编写）：
// .NET 8 经典 COM 互操作不支持 InterfaceIsIInspectable 的 RCW 强转，因此绕过 RCW，
// 直接读 vtable 第 25/26/27 槽生成委托调用（经 P0 实机验证）。
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop.Helpers;

internal class AudioPolicyConfigFactoryImpl : IAudioPolicyConfigFactory
{
    private const string ActivatableClassId = "Windows.Media.Internal.AudioPolicyConfig";
    private static readonly Guid Iid21H2 = new("ab3d4648-e242-459f-b02f-541c70306324");
    private static readonly Guid IidDownlevel = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");
    private const int SlotSet = 25;
    private const int SlotGet = 26;
    private const int SlotClear = 27;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetPersistedDefaultAudioEndpointDelegate(IntPtr @this, uint processId, EDataFlow flow, ERole role, IntPtr deviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPersistedDefaultAudioEndpointDelegate(IntPtr @this, uint processId, EDataFlow flow, ERole role, out IntPtr deviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ClearAllPersistedApplicationDefaultEndpointsDelegate(IntPtr @this);

    private readonly IntPtr _factory;
    private readonly SetPersistedDefaultAudioEndpointDelegate _set;
    private readonly GetPersistedDefaultAudioEndpointDelegate _get;
    private readonly ClearAllPersistedApplicationDefaultEndpointsDelegate _clear;

    internal AudioPolicyConfigFactoryImpl()
    {
        var iid = Environment.OSVersion.Version.Build >= 22000 ? Iid21H2 : IidDownlevel;
        Combase.WindowsCreateString(ActivatableClassId, (uint)ActivatableClassId.Length, out var hstring);
        try
        {
            var hr = Combase.RoGetActivationFactory(hstring, ref iid, out _factory);
            if (hr < 0)
                throw new InvalidComObjectException($"RoGetActivationFactory failed: 0x{hr:X8}");

            var vftbl = Marshal.PtrToStructure<IntPtr>(_factory);
            _set = Marshal.GetDelegateForFunctionPointer<SetPersistedDefaultAudioEndpointDelegate>(
                Marshal.PtrToStructure<IntPtr>(vftbl + IntPtr.Size * SlotSet));
            _get = Marshal.GetDelegateForFunctionPointer<GetPersistedDefaultAudioEndpointDelegate>(
                Marshal.PtrToStructure<IntPtr>(vftbl + IntPtr.Size * SlotGet));
            _clear = Marshal.GetDelegateForFunctionPointer<ClearAllPersistedApplicationDefaultEndpointsDelegate>(
                Marshal.PtrToStructure<IntPtr>(vftbl + IntPtr.Size * SlotClear));
        }
        finally
        {
            Combase.WindowsDeleteString(hstring);
        }
    }

    public HRESULT SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId)
        => (HRESULT)_set(_factory, processId, flow, role, deviceId);

    public HRESULT GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out string? deviceId)
    {
        var hr = (HRESULT)_get(_factory, processId, flow, role, out var hstring);
        deviceId = null;
        if (hr == HRESULT.S_OK && hstring != IntPtr.Zero)
        {
            var raw = Combase.WindowsGetStringRawBuffer(hstring, out var length);
            deviceId = Marshal.PtrToStringUni(raw, (int)length);
            Combase.WindowsDeleteString(hstring);
        }
        return hr;
    }

    public HRESULT ClearAllPersistedApplicationDefaultEndpoints()
        => (HRESULT)_clear(_factory);

    // agent 长生命周期，不实现 IDisposable；进程退出时 COM 指针随进程释放
}
