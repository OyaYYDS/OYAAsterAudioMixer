// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

public interface IAudioPolicyConfigFactory
{
    HRESULT SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId);
    HRESULT GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out string? deviceId);
    HRESULT ClearAllPersistedApplicationDefaultEndpoints();
}
