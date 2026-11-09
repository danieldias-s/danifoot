@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-local-historical-preview.ps1" -OpenBrowser
if errorlevel 1 pause
