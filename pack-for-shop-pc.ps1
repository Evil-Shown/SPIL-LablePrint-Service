# Build a folder you can copy to a normal shop PC (no Git, no Visual Studio).
# Output: dist\LabelPrintService\
# Copy that whole folder to the other PC, then double-click Start-LabelPrintService.bat

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "src\Spil.LabelPrint.Service\Spil.LabelPrint.Service.csproj"
$out = Join-Path $root "dist\LabelPrintService"

Write-Host "Publishing self-contained Windows build to $out"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish $proj -c Release -r win-x64 --self-contained true -o $out

Copy-Item (Join-Path $root "Start-LabelPrintService.bat") $out -Force
Copy-Item (Join-Path $root "SHOP-PC-README.txt") $out -Force

Write-Host ""
Write-Host "DONE. Copy this folder to the other PC:"
Write-Host "  $out"
Write-Host "On that PC, double-click Start-LabelPrintService.bat"
Write-Host "Then open http://localhost:5088/api/health"
