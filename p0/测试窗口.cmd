@echo off
chcp 65001 >nul
cd /d "%~dp0"
title AsterAudioP0 测试窗口
echo 已进入 P0 工具目录：%~dp0
echo.
echo 常用命令（详见 测试说明.md）：
echo   AsterAudioP0.exe whoami
echo   AsterAudioP0.exe list-devices
echo   AsterAudioP0.exe find chrome
echo   AsterAudioP0.exe get ^<pid^> render
echo   AsterAudioP0.exe set ^<pid^> render "设备名"
echo.
cmd /k
