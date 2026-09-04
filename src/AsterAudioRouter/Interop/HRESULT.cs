// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
namespace AsterAudioRouter.Interop;

public enum HRESULT : uint
{
    S_OK = 0x0,
    S_FALSE = 0x1,
    AUDCLNT_E_DEVICE_INVALIDATED = 0x88890004,
    AUDCLNT_S_NO_SINGLE_PROCESS = 0x889000d,
    ERROR_NOT_FOUND = 0x80070490,
    ERROR_INSUFFICIENT_BUFFER = 0x7a
}

public static class HResultExtensions
{
    public static string ToDisplay(this HRESULT hr) => $"{hr} (0x{(uint)hr:X8})";
}
