# 知识库：已有开源项目盘点（2026-09-04 调研）

> 结论 ✅：零件齐全（接口、封装、参考实现都有），没有现成的"ASTER + 按用户自动路由新进程"组合——本项目做的就是这部分增量。

| 项目 | 许可证 | 状态 | 与本项目关系 |
|---|---|---|---|
| [EarTrumpet](https://github.com/File-New-Project/EarTrumpet)（File-New-Project） | MIT | 活跃，~11.3k star | **移植来源**。每应用路由参考实现，interop 文件路径见 windows-per-app-audio-routing.md |
| [SoundSwitch](https://github.com/Belphemur/SoundSwitch)（Belphemur） | GPLv3 | 活跃 | 完整 vtable 槽位 25/26/27 定义；**代码不得抄入**（GPLv3），仅事实对照 |
| [audio-router](https://github.com/audiorouterdev/audio-router) | — | 已弃坑（2017） | 最早做每应用路由，新 Win10 上不稳，仅思路参考 |
| [CoreAudio](https://github.com/morphx666/CoreAudio)（morphx666） | — | 库 | C# CoreAudio 封装，含未公开接口封装 |
| spy-spotify（jwallet） | — | — | 借用了 EarTrumpet interop + AudioRouter.cs，可作第二印证 |
| [Soundpost](https://github.com/sathvik-zoldyck/Soundpost) | GPLv3 | 新项目 | 每应用路由还在 roadmap，未实现 |
| Volumey | GPLv3 | 活跃 | 只做热键音量/默认设备切换，不做每应用路由 |
| py-audio-splitter | — | — | 虚拟声卡（VB-Cable）路线，非改音量合成器 |

## 移植决策记录

- 2026-09-04 ✅：interop 以 EarTrumpet（MIT）为移植来源并保留版权注记；SoundSwitch（GPLv3）代码不抄入。用户已认可。
