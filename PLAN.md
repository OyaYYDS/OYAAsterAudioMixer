# ASTER 音频自动路由 — 方案与状态

> 状态更新：2026-09-05。P0/P1/P2-M1/P2-M2 均已完成并实机验证。

## 项目目标

按 Windows 用户账户名，把该用户新打开的应用程序自动路由到其专属扬声器/麦克风（等价于自动改音量合成器每应用选择）。面向 ASTER 一拖二/多座位：每座位 = 独立会话 + 独立账户，每座位跑一个实例，天然隔离。

## 已对齐的决策（用户拍板）

- 路由范围：该用户的**全部新进程**；仅在进程存在音频 Session 时路由（扫描音频会话驱动）
- 方向：扬声器 + 麦克风都做（UI 上麦克风下拉留待 M3，agent 已按规则处理 capture）
- 用户匹配：账户名精确匹配，`*` 通配作未匹配兜底
- 形态：**完整音量合成器**（托盘 + 应用列表 + 音量/静音 + 每应用设备下拉）
- 手动 vs 自动：**每应用自动/手动开关**（手动覆盖存 `%AppData%\AsterAudioRouter\overrides.json`，每用户独立）
- 黑名单：进程名 + `*` 通配（config.json 的 `exclude`）

## 里程碑状态

| 里程碑 | 状态 | 说明 |
|---|---|---|
| P0 双用户同程序实测 | ✅ 2026-09-05 | HKCU 每用户隔离成立；证据 `p0/p0logs/` |
| P1 每座位 agent + JSON 配置 | ✅ 2026-09-05 | 双座位运行正常（MoMo→AUX、85433→Input 全 S_OK） |
| P2-M1 合成器窗口 + 手动覆盖 | ✅ 2026-09-05 | 音量/静音/设备下拉/自动开关/托盘全部实测通过 |
| P2-M2 设置窗口 + 右键黑名单 | ✅ 2026-09-05 | 规则编辑/覆盖管理/自启开关/右键加黑名单 |
| P2-M3a 麦克风下拉 | ✅ 2026-09-05 | 每应用录音设备下拉（自动/系统默认/手动），覆盖按方向独立 |
| P2-M3b（可选） | ⏳ | 峰值表、默认设备切换、第二实例唤醒已开窗口 |

## 架构要点

- 单 exe 三模式：无参数=GUI（内置 agent 循环）、`--agent`=后台、`--status/--install/--uninstall/--reload/--dry-run`=CLI
- 每会话单实例互斥（Mutex `AsterAudioRouter_S<sessionId>`）
- 路由引擎：轮询本会话活动端点的音频会话（render 优先），对"有会话未路由"的进程调 `IAudioPolicyConfigFactory::SetPersistedDefaultAudioEndpoint`（render 设 eMultimedia+eConsole 两角色），30 秒重申兜底
- 覆盖变化（自动/手动切换）→ 清空路由记录 → ≤2 秒重新评估

## 关键实现教训（详见 knowledge/）

1. `manager.GetSimpleAudioVolume(Guid.Empty)` 取的是**本进程**的会话音量，还会给本进程创建幽灵会话 → 必须对会话对象 QI `ISimpleAudioVolume`，且同一 pid 的**全部会话**都要应用
2. .NET 的 `async Main` 不认 `[STAThread]` → WPF 必须用同步 Main
3. `InvariantGlobalization=true` 会让 WPF 绑定崩溃（找不到 en-us culture）
4. 枚举会话时 render 端点优先于 capture，去重后音量操作才作用在播放会话上

## 目录结构

```
p0/    P0 尖刺工具 + 验收记录（历史）
p1/    P1/P2 主程序（AsterAudioRouter.exe、config.json、双击脚本、日志、使用说明）
src/AsterAudioP0/        P0 CLI（调试工具保留）
src/AsterAudioRouter/    P1/P2 主程序源码（Interop/ Agent/ Gui/ 等）
knowledge/               研究知识库（置信度标注）
```
