// 版本分支：21H2（build 22000）起接口 IID 变化，分流在 AudioPolicyConfigFactoryImpl 内
using AsterAudioRouter.Interop.MMDeviceAPI;

namespace AsterAudioRouter.Interop.Helpers;

public static class AudioPolicyConfigFactory
{
    public static IAudioPolicyConfigFactory Create() => new AudioPolicyConfigFactoryImpl();
}
