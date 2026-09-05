@echo off
chcp 65001 >nul
cd /d "%~dp0"
start "" OYAAsterAudioMixer.exe --agent
timeout /t 2 >nul
