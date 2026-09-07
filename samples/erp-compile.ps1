# Compile one metro label for ERP (TSC TSPL in this example).
# Usage:
#   .\samples\erp-compile.ps1
#   .\samples\erp-compile.ps1 -BaseUrl http://labels-server:5088 -Brand zebra

param(
  [string]$BaseUrl = "http://localhost:5088",
  [string]$Brand = "tsc",
  [string]$PrinterHost = ""
)

$fields = Get-Content "$PSScriptRoot\erp-metro.json" -Raw | ConvertFrom-Json
$body = @{
  client = "erp"
  brand = $Brand
  layout = "metro"
  printerDpi = 300
  widthMm = 100
  heightMm = 150
  fields = $fields
} | ConvertTo-Json -Depth 8

$res = Invoke-RestMethod -Uri "$BaseUrl/api/labels/compile" -Method POST -ContentType "application/json" -Body $body
Write-Host "ok=$($res.ok) brand=$($res.brand) language=$($res.language) bytes=$($res.payload.Length)"
$res.payload | Set-Content -Encoding utf8NoBOM "$PSScriptRoot\erp-last-job.txt"

if ($PrinterHost) {
  Invoke-RestMethod -Uri "$BaseUrl/api/labels/send" -Method POST -ContentType "application/json" -Body (@{
    host = $PrinterHost
    port = 9100
    payload = $res.payload
  } | ConvertTo-Json)
}
