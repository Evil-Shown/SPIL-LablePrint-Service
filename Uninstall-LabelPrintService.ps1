# Remove the SPIL Label Print Windows Service.
# Run in an elevated PowerShell from this folder:
#   .\Uninstall-LabelPrintService.ps1

$ErrorActionPreference = "Stop"
$serviceName = "SpilLabelPrintService"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator (right-click PowerShell → Run as administrator)."
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service $serviceName is not installed."
    exit 0
}

if ($existing.Status -ne "Stopped") {
    Write-Host "Stopping $serviceName ..."
    Stop-Service -Name $serviceName -Force
    try {
        $existing.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    } catch { }
}

sc.exe delete $serviceName | Out-Null
Write-Host "Removed $serviceName. The program files were not deleted."
Write-Host "Delete this folder manually if you also want the files gone."
