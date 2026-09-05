# OYA ASTER 音量合成器（OYAAsterAudioMixer）

面向 **ASTER 一拖二/多座位 Windows 主机**的音量合成器 + 按用户自动音频路由。

每座位运行一个实例：**该座位任何程序一出声，自动路由到该用户配置的扬声器/麦克风**；同时提供完整的每应用音量合成器界面（音量/静音/每应用设备选择）。两座位完全独立，互不干扰。

## 特性

- 🎯 **按用户账户自动路由**：根据登录用户（精确匹配，`*` 通配兜底）把该用户的新进程自动路由到指定设备
- 🔇 **每应用音量/静音**：托盘点开即见本座位出声应用列表，滑条/静音实时生效
- 🎚️ **每应用设备下拉**（扬声器 + 麦克风各自独立）：`自动（跟随规则）` / `系统默认` / 指定设备
- 🔁 **手动覆盖机制**：手动改过的程序 agent 永不再碰（按方向独立），改回"自动"即交还；覆盖按用户存储，两座位互不影响
- 🚫 **黑名单**：进程名 + `*` 通配，右键应用即可加入
- ⚙️ **设置窗口**：路由规则编辑（热重载）、覆盖管理、开机自启开关
- 🗄️ **三角色全覆盖**：eMultimedia + eConsole + eCommunications（默认设备与默认通信设备场景都覆盖）
- 🖥️ **ASTER 原生适配**：每座位 = 独立会话 + 独立实例，单实例互斥按会话隔离

## 快速开始

1. 构建：
   ```
   dotnet publish src/AsterAudioRouter -c Release -o p1
   ```
2. 在 `p1\` 放 `config.json`（参考 `p1\config.example.json`），配置 用户 → 扬声器/麦克风/排除名单
3. 两个座位各自双击 `p1\OYAAsterAudioMixer.exe`（GUI + 自动路由一体），或 `--agent` 纯后台
4. 设置 → 其他 → 勾选开机自启（每座位各勾一次）

## 命令行

```
OYAAsterAudioMixer.exe --agent      后台运行（无窗口）
OYAAsterAudioMixer.exe --dry-run    试运行：只记录将执行的路由
OYAAsterAudioMixer.exe --status     查看配置/匹配规则/出声进程
OYAAsterAudioMixer.exe --install    写入本座位登录自启（HKCU Run）
OYAAsterAudioMixer.exe --uninstall  移除自启
OYAAsterAudioMixer.exe --reload     通知运行中的 agent 重新加载配置
```

配置热重载：改 `config.json` 保存即生效。手动覆盖：`%AppData%\AsterAudioRouter\overrides.json`。日志：`p1logs\`。

## 技术原理

- 每应用路由：Windows 内部 COM `IAudioPolicyConfigFactory::SetPersistedDefaultAudioEndpoint`（Win10 1803+，设置页"音量合成器"同款接口），按 exe 持久化、**按用户（HKCU）隔离**——已在真实 ASTER 一拖二环境实测"双用户同开同一程序互不串扰"（见 `p0/` 验收记录）
- 应用发现：轮询本会话全部活动端点的音频会话（`IAudioSessionManager2` 系列，公开 COM）
- 音量/静音：`ISimpleAudioVolume`（对会话对象 QI，覆盖该 pid 的全部会话）
- 技术细节与踩坑记录见 `knowledge/` 与 `PLAN.md`

## 致谢

- [EarTrumpet](https://github.com/File-New-Project/EarTrumpet)（MIT）——interop 代码的移植来源，`src/AsterAudioRouter/Interop/` 各文件头部保留其版权注记
- [SoundSwitch](https://github.com/Belphemur/SoundSwitch)（GPLv3）——vtable 槽位事实对照（未抄入代码）

## 作者

oyaYYDS

## 许可证

MIT License（见 LICENSE）
