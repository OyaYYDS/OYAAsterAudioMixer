// 移植自 EarTrumpet <https://github.com/File-New-Project/EarTrumpet>
// MIT License, Copyright (c) File-New-Project
using System;

namespace AsterAudioP0.Interop.MMDeviceAPI;

[Flags]
public enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
    ERole_enum_count = 3
}
