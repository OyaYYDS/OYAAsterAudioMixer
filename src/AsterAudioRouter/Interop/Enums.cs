// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System;

namespace AsterAudioRouter.Interop.MMDeviceAPI;

public enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
    EDataFlow_enum_count = 3
}

[Flags]
public enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
    ERole_enum_count = 3
}

public enum DeviceState : uint
{
    ACTIVE = 0x00000001,
    DISABLED = 0x00000002,
    NOTPRESENT = 0x00000004,
    UNPLUGGED = 0x00000008,
    MASK_ALL = 0x0000000f
}

public enum AudioSessionState
{
    Inactive = 0,
    Active = 1,
    Expired = 2
}

public enum AudioSessionDisconnectReason
{
    DeviceRemoval = 0,
    ServerShutdown = 1,
    FormatChanged = 2,
    SessionLogoff = 3,
    SessionDisconnected = 4,
    ExclusiveModeOverride = 5
}
