@echo off
title SPIL Label Print Service
cd /d "%~dp0"

echo Starting Label Print Service on http://localhost:5088
echo Keep this window open while printing from Opti.
echo.
echo Health:  http://localhost:5088/api/health
echo Swagger: http://localhost:5088/swagger
echo.

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:5088

if exist "Spil.LabelPrint.Service.exe" (
  "Spil.LabelPrint.Service.exe"
) else (
  echo ERROR: Spil.LabelPrint.Service.exe not found in this folder.
  echo Copy the whole LabelPrintService folder from the publish / dist build.
  pause
  exit /b 1
)
pause
