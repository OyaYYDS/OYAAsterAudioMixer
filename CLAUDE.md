# CLAUDE.md — ASTER 音频自动路由项目

## 项目目标

按 Windows 用户账户名，把该用户新打开的应用程序自动路由到其专属扬声器/麦克风（等价于自动改"设置→声音→音量合成器"里每应用的设备选择）。面向 ASTER 一拖二/多座位场景：每座位 = 独立会话 + 独立用户账户。

## 已对齐的决策（用户拍板，勿再讨论）

- 路由范围：该用户的**全部新进程**;仅在进程存在对应音频 Session 时执行应用级音频路由
- 方向：扬声器 + 麦克风**都做**
- 用户匹配：账户名**精确匹配**，配置支持 `*` 通配,作为未匹配用户的兜底规则
- 配置形态：**完整 GUI 配置界面**（P2 阶段）
- 方案文档：`PLAN.md`（已批准，含 P0 验收清单与失败预案）

## 工作规则

1. **里程碑门槛**：严格按 PLAN.md 推进。P0（双用户同 exe 互不串扰）验收全部通过前，禁止写 P1/P2（agent/GUI）代码。
2. **知识只存项目内（用户明确要求，2026-09-04）**：所有规则、方案、研究结论只写本工作区内的文件（CLAUDE.md / PLAN.md / knowledge/）。**禁止写 `~/.claude` 记忆、禁止写任何工作区外的状态文件。**
3. **知识库优先**：研究结论先查 `knowledge/` 文件夹；已在知识库中的事实直接引用，不重新搜索、不重复抓取、不在对话里复述大段内容。
4. **新结论入库**：高置信度的新研究要点写入 `knowledge/` 并标注置信度（✅ 高=官方文档或已验证 / 🟡 中=单一来源 / ⚠️ 未验证=待实测）。未验证的假设不得当作成立——尤其是"HKCU 每用户隔离"这一 P0 判据。
5. **网络**：本环境 WebFetch 被域名白名单拦截，抓取参考代码用 Bash `curl`（`cdn.jsdelivr.net`、`api.github.com` 已验证可通）。
6. **代码来源合规**：interop 只从 MIT 的 EarTrumpet 移植并保留版权注记；GPLv3 的 SoundSwitch 代码不得抄入项目，仅可作事实对照。
7. **语言**：与用户沟通、项目内文档一律中文。
8. **会话开场**：每次新会话先读 CLAUDE.md → PLAN.md → knowledge/。
9. **底层 API 优先参考已有实现**：涉及 `IAudioPolicyConfigFactory` / `SetPersistedDefaultAudioEndpoint` / Core Audio COM interop 时，优先以 EarTrumpet 的实际实现为准；不得凭记忆自行猜测 COM 接口、GUID、vtable 顺序或参数含义。涉及 Windows 内部/未公开 API 的结论必须标记为 🟡/⚠️，直到 P0 实机验证。

## 常用命令

- 构建/发布：`dotnet publish -c Release -o <项目根>\p1`（发布前先 `taskkill //F //IM AsterAudioRouter.exe` 停实例）
- GUI：双击 `p1\AsterAudioRouter.exe`；后台：`--agent`；CLI：`--status/--install/--uninstall/--reload/--dry-run`
- 热重载：改 `p1\config.json` 保存即生效；手动覆盖在 `%AppData%\AsterAudioRouter\overrides.json`
- 日志：`p1\p1logs\`；诊断音量问题看"音量操作/静音操作"行（会话数/设置/回读）
- 批处理文件注意：`.cmd` 必须 ASCII + CRLF（`sed -i 's/\r\?$/\r/'`）
