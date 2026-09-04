@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Removing autostart for current user...
AsterAudioRouter.exe --uninstall
pause
