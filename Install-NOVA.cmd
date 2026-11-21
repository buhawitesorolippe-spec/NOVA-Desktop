@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-NOVA.ps1"
if errorlevel 1 pause
