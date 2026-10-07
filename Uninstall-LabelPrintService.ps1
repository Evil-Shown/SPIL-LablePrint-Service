# Remove the SPIL Label Print Windows Service.
# Shop PCs: double-click Uninstall-LabelPrintService.bat

$ErrorActionPreference = "Stop"
$serviceName = "SpilLabelPrintService"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run Uninstall-LabelPrintService.bat and accept the Administrator prompt. Do not open the .ps1 file."
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
