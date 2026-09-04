# 知识库：Windows 每应用音频路由（内部接口）

> 置信度标注：✅ 高（官方文档/多源一致/已验证）｜🟡 中（单一来源）｜⚠️ 未验证（待 P0 实测）

## 核心接口：IAudioPolicyConfigFactory（Windows.Media.Internal.AudioPolicyConfig）

- 未公开文档的内部 COM 接口，微软"设置 → 声音 → 音量合成器/应用音量与设备首选项"页面自身即调用它。Win10 1803+ 可用。✅
- 方法（按 vtable 槽位）✅（SoundSwitch `AudioPolicyConfig.cs` 与 EarTrumpet 两套 Impl 一致）：
  - 25：`SetPersistedDefaultAudioEndpoint(uint pid, EDataFlow flow, ERole role, HSTRING deviceId)`
  - 26：`GetPersistedDefaultAudioEndpoint(uint pid, EDataFlow flow, ERole role, out HSTRING deviceId)`
  - 27：`ClearAllPersistedApplicationDefaultEndpoints()`
- 激活方式（两种，实现二选一）✅：
  - EarTrumpet 方式：`RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig", ref iid, out factory)`
  - SoundSwitch 方式：`DllGetActivationFactory` + `IInspectable::GetIids` 动态取最后一个 IID 并校验白名单（更抗版本漂移）
- 接口 IID 按系统构建分两套 ✅（SoundSwitch 白名单 + EarTrumpet 双 Impl 印证）：
  - 21H2 及以后（含 Win11）：`ab3d4648-e242-459f-b02f-541c70306324`
  - 旧版（21H2 之前）：`2a59116d-6c4f-45e0-a74f-707e3fef9258`
- 设置时应同时设置 `eMultimedia` 和 `eConsole` 两个角色（EarTrumpet 行为；设置页选完音频立刻切走的原因）✅；本项目（2026-09-05 起）追加 `eCommunications` 第三角色以覆盖"默认通信设备"场景（best-effort，失败仅记一次日志）✅
- 对已运行进程设置后实时生效（微软设置页行为一致）✅

## 持久化语义（本项目 P0 判据所在）

- 每应用分配存储位置：`HKCU\Software\Microsoft\Multimedia\Audio\DefaultEndpoint` 🟡（StackOverflow 单一来源）
- 推断：按用户隔离 → 两座位同开同一 exe 互不覆盖 ⚠️ **未验证，必须 P0 实测；不成立则按 PLAN.md 失败预案降级**
- HRESULT：进程尚无音频会话时返回 `PROCESS_NO_AUDIO`（具体数值待实现时从 EarTrumpet HRESULT 定义抓取）🟡

## 已知限制

- 独占模式（WASAPI exclusive）/ ASIO / 游戏内置设备选择的程序可能无视该设置 🟡（EarTrumpet README 所述）
- 蓝牙/USB 设备重连可能重置分配 🟡
- UWP 应用是否被覆盖：设置页可见可选，实际效果待 P0 抽测 ⚠️
- 未公开接口：微软跨构建可能变 IID/vtable → 移植 EarTrumpet 双版本兼容方案，系统更新后回归 ✅（EarTrumpet 已实践多年）

## 跨框架移植要点（本项目 2026-09-04 实测 ✅）

在 .NET 8 上移植 EarTrumpet（.NET Framework）interop 踩到两个坑，已解决并实机验证：

1. **P/Invoke 不支持 `[MarshalAs(UnmanagedType.HString)]`**：运行时抛 `MarshalDirectiveException`（.NET Core 起移除 HSTRING 的 P/Invoke 封送）。
   → 激活路径改手动 HSTRING：`WindowsCreateString`/`WindowsDeleteString` + `RoGetActivationFactory` 裸指针。
2. **经典 COM 互操作不支持 RCW 强转 `InterfaceIsIInspectable` 接口**：抛 `PlatformNotSupportedException`。
   → 完全绕开 RCW：对 `RoGetActivationFactory` 返回的裸接口指针直接读 vtable 第 25/26/27 槽生成 StdCall 委托调用；HSTRING 出入参全部手动（`WindowsGetStringRawBuffer` + `Marshal.PtrToStringUni`）。
   → 实机调用已通：对不存在的 pid 返回 `0x80070057`（E_INVALIDARG），证明调用真实到达 API。
3. **设备枚举**：`Windows.Devices.Enumeration`（公开 WinRT API）的 `DeviceInformation.Id` 即 SWD 格式设备 ID，与 SetPersistedDefaultAudioEndpoint 期望格式一致（Win11 26200 实测枚举输出与 EarTrumpet 的 SWD 包装规则完全吻合）✅。
4. **本机即 ASTER 一拖二实机**（2026-09-04 实测）：session 2 与 session 3 各有 explorer 进程同时在跑；当前工作会话为 session 3（user=85433）。跨会话可读 pid/session，进程路径访问被拒（正常权限隔离）✅。

## 来源

- EarTrumpet 相关文件（MIT）：`Interop/Helpers/AudioPolicyConfigFactory*.cs`、`Interop/MMDeviceAPI/IAudioPolicyConfigFactory*.cs`
- SoundSwitch `SoundSwitch.Audio.Manager/Interop/Client/Extended/AudioPolicyConfig.cs`（GPLv3，仅对照）
- StackOverflow 72399817
- 本项目实测记录：`src/AsterAudioP0/` 源码与 `p0/p0logs/`
