@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Installing autostart for current user...
AsterAudioRouter.exe --install
pause
