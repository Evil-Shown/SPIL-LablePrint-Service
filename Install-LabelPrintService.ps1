# Install SPIL Label Print Service as a Windows Service (auto-start, no console).
# Shop PCs: double-click Install-LabelPrintService.bat (do not open this .ps1 in VS Code).
# Other PCs must reach this host?  double-click Install-LabelPrintService-ListenLan.bat

[CmdletBinding()]
param(
    [switch]$ListenLan,
    [int]$Port = 5088
)

$ErrorActionPreference = "Stop"
$serviceName = "SpilLabelPrintService"
$displayName = "SPIL Label Print Service"
$scriptPath = $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptPath)) {
    throw @"
Do not paste this script into PowerShell. That is why Path is null.

On the shop PC:
  1. Open the LabelPrintService folder (it contains Spil.LabelPrint.Service.exe).
  2. Double-click Install-LabelPrintService.bat
  3. Click Yes on the Administrator prompt.

If the .bat opens in Notepad or VS Code: right-click it → Run as administrator.
"@
}
$root = Split-Path -Parent $scriptPath
$exe = Join-Path $root "Spil.LabelPrint.Service.exe"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run Install-LabelPrintService.bat and accept the Administrator prompt. Do not open the .ps1 file."
}

if (-not (Test-Path $exe)) {
    throw "Spil.LabelPrint.Service.exe not found in $root. Copy the whole published folder first."
}

$listenHost = if ($ListenLan) { "0.0.0.0" } else { "127.0.0.1" }
$urls = "http://${listenHost}:${Port}"

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing $serviceName ..."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $serviceName -Force
        $existing.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating $displayName"
Write-Host "  exe: $exe"
Write-Host "  url: $urls"

New-Service `
    -Name $serviceName `
    -BinaryPathName "`"$exe`"" `
    -DisplayName $displayName `
    -Description "Compiles Opti/ERP labels and can send them to printers on TCP 9100. Listens on $urls" `
    -StartupType Automatic | Out-Null

$reg = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
Set-ItemProperty -Path $reg -Name Environment -Value @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ASPNETCORE_URLS=$urls"
) -Type MultiString

sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
sc.exe failureflag $serviceName 1 | Out-Null

if ($ListenLan) {
    $ruleName = "SPIL Label Print Service"
    $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if (-not $rule) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
        Write-Host "Opened Windows Firewall TCP $Port (inbound)"
    }
}

Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(30))

$healthUrl = "http://127.0.0.1:${Port}/api/health"
$ok = $false
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Milliseconds 700
    try {
        $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
        if ($health.ok) { $ok = $true; break }
    } catch { }
}

Write-Host ""
if ($ok) {
    Write-Host "Installed and running. Health: $healthUrl"
} else {
    Write-Host "Service is started, but health check did not respond yet."
    Write-Host "Check Event Viewer → Windows Logs → Application (source SpilLabelPrintService)."
    Write-Host "Then open $healthUrl"
}

Write-Host "Opti / Label Crafter Service URL: http://localhost:$Port"
if ($ListenLan) {
    Write-Host "Other PCs: http://<THIS-PC-LAN-IP>:$Port"
}
Write-Host "The service starts automatically when Windows boots. Users do not need to log in or open a window."
