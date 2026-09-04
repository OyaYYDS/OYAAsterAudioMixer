@echo off
chcp 65001 >nul
cd /d "%~dp0"
AsterAudioRouter.exe --dry-run
