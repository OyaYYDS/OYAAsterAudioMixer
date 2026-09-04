# 知识库：P2 音量合成器实现要点（2026-09-05 实机验证 ✅）

> 置信度标注：✅ 高（本项目实机踩坑验证）｜🟡 中

## 音量/静音（ISimpleAudioVolume）两大坑 ✅

1. **不能用 `IAudioSessionManager2.GetSimpleAudioVolume(Guid.Empty)`**：
   - 按文档它返回"调用者自己进程"的会话音量，且调用时会**为本进程创建音频会话**
   - 实测后果：合成器列表出现"AsterAudioRouter"幽灵行；所有应用行的音量/静音联动（拿到同一个对象）；设置无任何可闻效果
   - 正确姿势：对会话对象 QI：`(ISimpleAudioVolume)session`（IAudioSessionControl QI 到 ISimpleAudioVolume，每会话独立）
2. **同一 pid 可能持有多个会话**（chrome 实测 2 个：播放+录音）：
   - 按 pid 去重只取一个会话 → 可能取到不出声的那个 → 音量设置返回 S_OK 但无声
   - 正确姿势：音量/静音**应用到该 pid 的全部会话**（与 Windows 每应用音量语义一致）；枚举时 render 端点先于 capture，显示/回读取第一个
   - 诊断手段：日志"音量操作 pid=.. 会话数=N 设置=X 回读=Y"——回读=设置 说明 Set 生效；会话数>1 说明多会话

## WPF 移植两大坑 ✅

1. **`async Main` 不认 `[STAThread]`**：入口被编译器改写在线程池执行 → WPF 创建 Window 报 "The calling thread must be STA"。修法：Main 保持同步 + `[STAThread]`，需要 await 的内部用 `.GetAwaiter().GetResult()` 阻塞。
2. **`InvariantGlobalization=true` 破坏 WPF 绑定**：报 "Cannot find non-neutral culture related to 'en-us'"（绑定默认取 UI culture）。WPF 项目必须去掉该开关。

## 其他工程要点 ✅

- WPF 自定义入口：`<EnableDefaultApplicationDefinition>false>`，App.xaml 自动变为 Page，手动 `app.InitializeComponent(); window.Show(); app.Run()`；`ShutdownMode=OnExplicitShutdown`（托盘退出自管）
- `UseWindowsForms=true` 时 `System.Windows.Forms` 进入隐式 using → `Application/CheckBox/ComboBox/MessageBox` 与 WPF 冲突，用 using 别名（别名优先于命名空间）或全限定名
- `ExtractAssociatedIcon` 取 exe 图标转 BitmapImage 需 `Freeze()` 后跨线程/缓存使用；`GetIconPath` 常为空，需回退 exe 路径
- 托盘：WinForms NotifyIcon（零依赖）；点 X 隐藏到托盘（Closing 里 e.Cancel=true + Hide()）
- 单实例：Mutex 名带 sessionId（`AsterAudioRouter_S<session>`），只限长驻模式（GUI/--agent），CLI 一次性命令不受限
- 覆盖联动：OverridesStore.Changed → agent 用 Interlocked 置脏标记 → 循环线程内清空 `_routed` → 下一轮扫描（≤2s）按新覆盖状态重路由（避免跨线程改集合）

## 手动覆盖机制（设计定型 ✅）

- 存储：`%AppData%\AsterAudioRouter\overrides.json`，**每用户独立**（两座位互不影响）
- 语义（**按方向独立**，2026-09-05 升级）：`Playback`/`Capture` 字段各为 `null=自动`、`""=系统默认`、`"设备名"=手动设备`；两方向都 null 的记录自动删除；agent 只跳过被覆盖的方向
- GUI 改设备：立即 SetPersisted + 写覆盖；改回自动：该方向交还 + agent ≤2s 重路由
- 旧格式兼容：旧记录里 `""` 语义不变（系统默认）、设备名不变（手动设备），缺字段按 null（自动）处理
- 黑名单（config `exclude`）：进程名 + `*` 通配（`RouterConfig.WildcardMatch`），右键"加入黑名单"写入当前用户命中规则的 exclude
