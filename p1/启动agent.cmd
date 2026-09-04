@echo off
chcp 65001 >nul
cd /d "%~dp0"
start "" AsterAudioRouter.exe --agent
timeout /t 2 >nul
