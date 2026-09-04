// 版本分支：21H2（build 22000）起接口 IID 变化，分流在 AudioPolicyConfigFactoryImpl 内
using AsterAudioP0.Interop.MMDeviceAPI;

namespace AsterAudioP0.Interop.Helpers;

public static class AudioPolicyConfigFactory
{
    public static IAudioPolicyConfigFactory Create() => new AudioPolicyConfigFactoryImpl();
}
