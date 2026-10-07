@echo off
title Install SPIL Label Print Service
cd /d "%~dp0"

:: Double-click this file. Do not open the .ps1 in VS Code / Notepad.
net session >nul 2>&1
if %errorLevel% neq 0 (
  echo Requesting Administrator permission...
  powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
  exit /b
)

echo Installing Windows Service (always running, starts at boot)...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-LabelPrintService.ps1" %*
echo.
pause
