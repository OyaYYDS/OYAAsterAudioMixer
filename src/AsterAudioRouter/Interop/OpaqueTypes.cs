// 占位声明：这些接口只出现在签名里（方法从不被本项目调用），故仅声明 GUID 与 IUnknown 布局。
// GUID 来源：EarTrumpet <https://github.com/File-New-Project/EarTrumpet> MIT License / 微软文档。
using System;
using System.Runtime.InteropServices;

namespace AsterAudioRouter.Interop
{
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore { }
}

namespace AsterAudioRouter.Interop.MMDeviceAPI
{
    [ComImport]
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient { }

    [ComImport]
    [Guid("C3B284D4-6D39-4359-B3CF-B56DDB3BB39C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioVolumeDuckNotification { }
}
