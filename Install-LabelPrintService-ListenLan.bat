@echo off
title Install SPIL Label Print Service (LAN)
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
  echo Requesting Administrator permission...
  powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
  exit /b
)

echo Installing Windows Service so OTHER PCs can call this machine...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-LabelPrintService.ps1" -ListenLan
echo.
pause
