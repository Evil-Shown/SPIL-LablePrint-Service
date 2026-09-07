# SPIL Label Print Service

HTTP API that turns label JSON into printer code: **ZPL, TSPL, EZPL, SBPL, DPL, EPL**.

**Canonical source:** this folder — `D:\Coding\SPIL LABS\Repos\Lable Print Service`  
GitHub: [github.com/Evil-Shown/SPIL-LablePrint-Service](https://github.com/Evil-Shown/SPIL-LablePrint-Service)

Do **not** develop in `SPIL DOC\LABLE PRINT SERVICE`. That copy is docs/backup only and will go stale. Build and pack shop PCs from **this** repo (`pack-for-shop-pc.ps1`).

**Two independent clients — Opti and ERP.** Same endpoints, **different templates and field bags**. Do not mix them.

| Client | `client` | Typical `layout` | What they send |
|--------|----------|------------------|----------------|
| Opti / Opti-TV | `opti` | `template` | That app’s Labels-editor JSON + piece `labelData` |
| ERP | `erp` | `metro` **or** `template` | Built-in glass `fields`, **or ERP’s own** template JSON + `labelData` |

**Typical flow:** client → `POST /api/labels/compile` → read `payload` (or `zpl`) → send those bytes to the printer on **TCP 9100**.

Interactive API docs: **http://localhost:5088/swagger**

Repo: [github.com/Evil-Shown/SPIL-LablePrint-Service](https://github.com/Evil-Shown/SPIL-LablePrint-Service)

---

## Contents

1. [What you need](#what-you-need)
2. [Project layout](#project-layout)
3. [Run locally (dev / test)](#run-locally-dev--test)
4. [Who uses this (Opti vs ERP)](#who-uses-this-opti-vs-erp)
5. [ERP integration](#erp-integration)
6. [Test the API](#test-the-api)
7. [Deploy to IIS (production)](#deploy-to-iis-production)
8. [Configuration](#configuration)
9. [API reference](#api-reference)
10. [Template compile (any Labels JSON)](#template-compile-any-labels-json)
11. [Metro layout (ERP built-in)](#metro-layout-erp-built-in)
12. [Batch printing](#batch-printing)
13. [Optional: send from the server](#optional-send-from-the-server)
14. [Printer brands](#printer-brands)
15. [Troubleshooting](#troubleshooting)

---

## What you need

### To build and publish

| Item | Notes |
|------|--------|
| **.NET 8 SDK** | Build machine. [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Windows** | Service targets `net8.0-windows` (uses `System.Drawing` for text/images) |

### On the IIS server

| Item | Notes |
|------|--------|
| **Windows Server** or Windows 10/11 Pro with IIS | |
| **IIS** | Web Server role + **ASP.NET** (Application Development → ASP.NET 4.8 is fine; app is Core) |
| **.NET 8 Hosting Bundle** | **Required.** Installs ASP.NET Core Module for IIS. [Download](https://dotnet.microsoft.com/download/dotnet/8.0) → *Hosting Bundle* |
| **Firewall** | Open the HTTP port you bind (e.g. **5088** or **80**). Printer port **9100** only if you use `/api/labels/send` from this server |

After installing the Hosting Bundle, run:

```powershell
iisreset
```

### Optional (for printing from clients)

- Label printer on the LAN that accepts **raw TCP 9100** (ZPL, TSPL, EZPL, SBPL, or DPL depending on `brand`)
- Opti already has `send_zpl_tcp` on port 9100; ERP should use `TcpClient` the same way

---

## Project layout

```
Lable Print Service/
├── Spil.LabelPrint.Service.sln
├── README.md
├── samples/
│   ├── lbl004-piece.json          # Opti-shaped piece data
│   ├── erp-metro.json             # ERP metro field bag
│   └── erp-compile.ps1            # ERP smoke test
└── src/Spil.LabelPrint.Service/
```

---

## Run locally (dev / test)

```powershell
cd "D:\Coding\SPIL LABS\Repos\Lable Print Service\src\Spil.LabelPrint.Service"
dotnet restore
dotnet build
dotnet run
```

Default URL: **http://localhost:5088**

Health check:

```powershell
Invoke-RestMethod http://localhost:5088/api/health
```

Expected:

```json
{
  "ok": true,
  "service": "Spil.LabelPrint",
  "languages": ["zpl", "tspl", "ezpl", "sbpl", "dpl", "epl"],
  "brands": ["zebra", "honeywell", "citizen", "sato", "sato-sbpl", "tsc", "godex", "datamax", "epl"],
  "docs": "/swagger"
}
```

Swagger UI: **http://localhost:5088/swagger**

Brand catalog (for ERP printer dropdowns):

```powershell
Invoke-RestMethod http://localhost:5088/api/brands
```

### CLI compile (no HTTP)

Useful to verify a template file before IIS deploy:

```powershell
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
dotnet run --no-launch-profile -- --compile "C:\Users\TUF\Downloads\LBL_004.json" ".\..\..\samples\lbl004-piece.json" > test.zpl
```

You should get a file starting with `^XA` and containing `^XZ`.

---

## Who uses this (Opti vs ERP)

The service does not store templates. Each request carries **that client’s** layout and data.

- **Opti** never sends ERP metro `fields`. **ERP** never needs Opti’s `LBL_004.json` unless ERP explicitly chooses to reuse that file.
- Field keys follow **the template in the request**, not a shared Opti/ERP dictionary.
- `layout: "metro"` always uses the built-in ERP glass layout and **ignores** `template` on that call.
- `layout: "template"` compiles only the JSON posted as `template`.

---

## ERP integration

ERP does **not** need Opti running, and does **not** have to use Opti’s label file.

### Choose a path

| ERP has… | Use |
|----------|-----|
| Its own Labels JSON (different from Opti) | `client: "erp"`, `layout: "template"`, that file as `template` + piece `labelData` |
| No template file (fixed glass layout) | `client: "erp"`, `layout: "metro"` + `fields` (`samples/erp-metro.json`) |
| Several labels in one job | `POST /api/labels/compile-batch` with the same `client` / `layout` |

### 1. Compile

```http
POST /api/labels/compile
Content-Type: application/json
```

```json
{
  "client": "erp",
  "brand": "tsc",
  "layout": "metro",
  "printerDpi": 300,
  "widthMm": 100,
  "heightMm": 150,
  "fields": {
    "orderNo": "38551 /2",
    "custOrderNo": "DS-002",
    "jobDescription": "ACTIVE ALUMINIUM WINDOWS",
    "dimensions": "1120 X 980",
    "glassSpec": "4.00mm Clear Float",
    "deliveryDate": "Tue 1/9",
    "sqm": "2.20",
    "lineRef": "5 / 2",
    "route": "Route 4",
    "weightKg": "10.98",
    "processNotes": ["Flat Polish"],
    "barcodeValue": "L-302925-1"
  }
}
```

`brand` values: `zebra`, `honeywell`, `citizen`, `sato`, `sato-sbpl`, `tsc`, `godex`, `datamax`, `epl`.  
List them at runtime: `GET /api/brands`.

**Response**

```json
{
  "ok": true,
  "language": "tspl",
  "brand": "tsc",
  "payload": "SIZE 100 mm,150 mm\n...",
  "zpl": "SIZE 100 mm,150 mm\n...",
  "widthDots": 1200,
  "heightDots": 1800,
  "dpmm": 12
}
```

Use **`payload`** in new ERP code (`zpl` is the same string, kept for Opti).

PowerShell smoke test:

```powershell
.\samples\erp-compile.ps1 -BaseUrl http://localhost:5088 -Brand tsc
```

### 2. Send to the printer (from the ERP machine)

Open a raw TCP socket to the printer **IP:9100** and write the payload as UTF-8 (the job is ASCII). Do **not** wrap it in a Windows print driver.

**C#**

```csharp
using var http = new HttpClient { BaseAddress = new Uri("http://labels-server:5088") };
using var compile = await http.PostAsJsonAsync("/api/labels/compile", new
{
    brand = "tsc",
    layout = "metro",
    printerDpi = 300,
    fields = new {
        orderNo = "38551 /2",
        barcodeValue = "L-302925-1",
        jobDescription = "ACTIVE ALUMINIUM WINDOWS",
        dimensions = "1120 X 980",
        glassSpec = "4.00mm Clear Float",
    },
});
compile.EnsureSuccessStatusCode();
var job = await compile.Content.ReadFromJsonAsync<CompileResult>();
var bytes = Encoding.UTF8.GetBytes(job!.Payload ?? job.Zpl);

using var tcp = new TcpClient();
await tcp.ConnectAsync("192.168.1.50", 9100);
await tcp.GetStream().WriteAsync(bytes);

record CompileResult(bool Ok, string Language, string Brand, string Payload, string Zpl);
```

**Python**

```python
import json, socket, urllib.request

req = urllib.request.Request(
    "http://labels-server:5088/api/labels/compile",
    data=json.dumps({
        "brand": "zebra",
        "layout": "metro",
        "printerDpi": 300,
        "fields": {"orderNo": "38551 /2", "barcodeValue": "L-302925-1"},
    }).encode(),
    headers={"Content-Type": "application/json"},
)
with urllib.request.urlopen(req) as r:
    payload = json.load(r)["payload"]

s = socket.create_connection(("192.168.1.50", 9100), timeout=8)
s.sendall(payload.encode("utf-8"))
s.close()
```

### 3. Optional: let the service print

Only if the **IIS server can reach the printer**:

```json
POST /api/labels/send
{
  "host": "192.168.1.50",
  "port": 9100,
  "compile": {
    "brand": "zebra",
    "layout": "metro",
    "printerDpi": 300,
    "fields": { "orderNo": "38551 /2", "barcodeValue": "L-302925-1" }
  }
}
```

Prefer compiling on the server and printing from ERP (`TcpClient`) so printer firewall rules stay on the shop-floor PC.

### 4. Same Opti template as production labels

Export/copy the Opti Labels JSON, POST it as `template` with each piece as `labelData`. Field keys must match `{{tokens}}` / `fieldKey` / `fieldMappings` (same as Opti). Example piece: `samples/lbl004-piece.json`.

---

## Test the API

### 1. Health

```powershell
Invoke-RestMethod http://localhost:5088/api/health
```

### 2. Small template compile

```powershell
$body = @{
  brand = "zebra"
  layout = "template"
  printerDpi = 300
  template = @{
    width = 100
    height = 150
    sections = @{
      body = @{
        enabled = $true
        fields = @(
          @{
            type = "text"
            x = 8; y = 8; width = 360; height = 28
            fontSize = 18
            fontWeight = "bold"
            fieldKey = "customerName"
            value = "{{customerName}}"
          },
          @{
            type = "barcode"
            x = 200; y = 80; width = 160; height = 60
            fontSize = 8
            fieldKey = "Barcode"
            displayValue = $true
            source = @("Barcode", "barcode")
          }
        )
      }
    }
  }
  labelData = @{
    customerName = "VAHID HELDOV"
    Barcode = "L-69515-1"
  }
} | ConvertTo-Json -Depth 20

Invoke-RestMethod `
  -Uri http://localhost:5088/api/labels/compile `
  -Method POST `
  -ContentType "application/json" `
  -Body $body
```

Check response: `ok: true`, `zpl` starts with `^XA`, `dpmm: 12` for 300 DPI.

### 3. Full LBL_004 template

```powershell
$template = Get-Content "C:\Users\TUF\Downloads\LBL_004.json" -Raw | ConvertFrom-Json
$labelData = Get-Content ".\samples\lbl004-piece.json" -Raw | ConvertFrom-Json

$body = @{
  brand = "zebra"
  layout = "template"
  printerDpi = 300
  template = $template
  labelData = $labelData
} | ConvertTo-Json -Depth 30

$res = Invoke-RestMethod `
  -Uri http://localhost:5088/api/labels/compile `
  -Method POST `
  -ContentType "application/json" `
  -Body $body

$res.ok                    # True
$res.zpl.Length -gt 10000  # True (logo is embedded)
$res.zpl -match "L-69515-1"  # barcode value present
```

Save ZPL to file:

```powershell
$res.zpl | Set-Content -Encoding utf8NoBOM label.zpl
```

Send to a printer (from Opti or any TCP 9100 client), or use the virtual printer script in `spil-opti/scripts/zpl_virtual_printer.py`.

---

## Deploy to IIS (production)

### Step 1 — Publish the app

On a machine with the .NET 8 SDK:

```powershell
cd "D:\Coding\SPIL LABS\Repos\Lable Print Service\src\Spil.LabelPrint.Service"

dotnet publish -c Release -o C:\inetpub\LabelPrintService
```

This folder must contain at least:

- `Spil.LabelPrint.Service.dll`
- `web.config`
- `appsettings.json`
- All dependency DLLs

### Step 2 — Install Hosting Bundle on the server

1. Download **.NET 8.0 Hosting Bundle** (not just runtime).
2. Install on the IIS machine.
3. `iisreset`

Verify:

```powershell
dotnet --list-runtimes
```

You should see `Microsoft.AspNetCore.App 8.x` and `Microsoft.NETCore.App 8.x`.

### Step 3 — Create IIS site

1. Open **IIS Manager**.
2. **Application Pools** → **Add Application Pool**
   - Name: `LabelPrintService`
   - **.NET CLR version:** **No Managed Code**
   - Managed pipeline: **Integrated**
   - Start application pool immediately: ✓
3. **Sites** → **Add Website**
   - Site name: `LabelPrintService`
   - Application pool: `LabelPrintService`
   - Physical path: `C:\inetpub\LabelPrintService`
   - Binding: e.g. `http`, port **5088**, host name blank (or `labels.yourcompany.local`)
4. Select the site → **Browse** or open `http://server-name:5088/api/health`

### Step 4 — Permissions and logs

Create a `logs` folder next to the DLL (for stdout from `web.config`):

```powershell
New-Item -ItemType Directory -Force C:\inetpub\LabelPrintService\logs
```

Grant the app pool identity **Modify** on `logs` (and **Read** on the site folder):

- App pool identity is usually `IIS AppPool\LabelPrintService`
- Or use ApplicationPoolIdentity (default)

In IIS → site → **Authentication**: usually **Anonymous** enabled is enough (no login required for internal LAN API).

### Step 5 — Production appsettings (optional)

Edit `C:\inetpub\LabelPrintService\appsettings.json`:

```json
{
  "LabelPrint": {
    "DefaultDpmm": 12,
    "DefaultWidthMm": 100,
    "DefaultHeightMm": 150,
    "AllowTcpSend": false
  }
}
```

Set `AllowTcpSend: false` if clients always print themselves and you do not want the server opening TCP to printers.

### Step 6 — Firewall

```powershell
New-NetFirewallRule -DisplayName "Label Print Service" -Direction Inbound -Protocol TCP -LocalPort 5088 -Action Allow
```

Replace `5088` with your binding port.

### Step 7 — Smoke test on server

```powershell
Invoke-RestMethod http://localhost:5088/api/health
```

From another PC on the LAN:

```powershell
Invoke-RestMethod http://YOUR-SERVER:5088/api/health
```

---

## Configuration

File: `appsettings.json` (copied to publish folder).

| Key | Default | Meaning |
|-----|---------|---------|
| `LabelPrint:DefaultDpmm` | `12` | 12 dpmm = 300 DPI |
| `LabelPrint:DefaultWidthMm` | `100` | Metro layout default width |
| `LabelPrint:DefaultHeightMm` | `150` | Metro layout default height |
| `LabelPrint:AllowTcpSend` | `true` | If `false`, `POST /api/labels/send` returns 403 |

**DPI → dpmm** (can also pass `printerDpi` or `dpmm` per request):

| Printer DPI | `dpmm` |
|-------------|--------|
| 203 | 8 |
| 300 | 12 |
| 600 | 24 |

---

## API reference

Base URL examples: `http://localhost:5088` or `http://labels-server:5088`

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/health` | Service up, supported brands |
| `GET` | `/api/brands` | Brand → language catalog for ERP dropdowns |
| `GET` | `/swagger` | Interactive OpenAPI (try compile from the browser) |
| `POST` | `/api/labels/compile` | One label → printer code (`payload` / `zpl`) |
| `POST` | `/api/labels/compile-batch` | Many labels → one concatenated job |
| `POST` | `/api/labels/send` | Compile (optional) + TCP send to printer |

All POST bodies: `Content-Type: application/json`. Property names are **camelCase**.

### `POST /api/labels/compile`

**Request (template layout):**

| Field | Required | Description |
|-------|----------|-------------|
| `client` | No | `opti` or `erp` — which product is compiling (templates are not shared) |
| `layout` | No | `"template"` (JSON in this request) or `"metro"` (built-in ERP layout only) |
| `brand` | No | `zebra` \| `honeywell` \| `citizen` \| `sato` \| `sato-sbpl` \| `tsc` \| `godex` \| `datamax` \| `epl` — service generates that language |
| `printerDpi` | No | e.g. `300` → 12 dpmm |
| `dpmm` | No | Override: `6`, `8`, `12`, or `24` |
| `template` | Yes* | This client’s Labels JSON (`width`/`height`/`sections`). Opti and ERP files differ |
| `labelData` | Yes* | Piece / order values for **that** template |
| `configuration` | No | Optional; Opti sends its config, ERP may omit |
| `project` | No | Optional Opti project (ERP usually omits) |
| `fields` | Yes** | Metro layout field bag (ERP built-in only) |

\* Required for `layout=template`.  
\** Required for `layout=metro`.

**Response:**

```json
{
  "ok": true,
  "client": "opti",
  "layout": "template",
  "language": "zpl",
  "brand": "zebra",
  "payload": "^XA\n^CI28\n...",
  "zpl": "^XA\n^CI28\n...",
  "widthDots": 1200,
  "heightDots": 1800,
  "dpmm": 12
}
```

**Errors:** HTTP 400 with `{ "ok": false, "error": "message" }`

### `POST /api/labels/compile-batch`

Same as compile, but multiple pieces:

```json
{
  "brand": "zebra",
  "layout": "template",
  "printerDpi": 300,
  "template": { },
  "labels": [
    { "customerName": "A", "Barcode": "L-1" },
    { "customerName": "B", "Barcode": "L-2" }
  ]
}
```

Response: `{ "ok": true, "count": 2, "language": "zpl", "brand": "zebra", "zpl": "^XA...^XZ^XA...^XZ" }` (`zpl` holds TSPL/EZPL/SBPL/DPL when `brand` is tsc/godex/sato-sbpl/datamax)

### `POST /api/labels/send`

Compile on the server and push to printer (only if server can reach printer IP):

```json
{
  "host": "192.168.1.50",
  "port": 9100,
  "compile": {
    "layout": "template",
    "printerDpi": 300,
    "template": { },
    "labelData": { "Barcode": "L-1" }
  }
}
```

Or send ready ZPL:

```json
{
  "host": "192.168.1.50",
  "port": 9100,
  "zpl": "^XA...^XZ"
}
```

---

## Template compile (any Labels JSON)

POST the **entire** template JSON for **this client only**. Opti sends Opti’s file; ERP sends ERP’s file. Same JSON *shape* (`sections.fields`), different content and field keys.

- `width`, `height` (mm)
- `sections` → `{ sectionName: { enabled, fields: [...] } }`
- `fieldMappings` (optional) — `noteField`, `subField`, `isBlackBox`
- Field types: `text`, `header`, `barcode`, `image`, `line`, `shape`, `qrcode`, `machineschecklist`

**Value resolution** (same as Opti):

1. `{{token}}` in field `value`
2. `labelData[fieldKey]`
3. `fieldMappings` → `noteN.fieldM`
4. Black-box styling from `isBlackBox` or `#000` background + white text

**Example `labelData`:** see `samples/lbl004-piece.json`.

**Opti (JavaScript):**

```js
const res = await fetch("http://labels-server:5088/api/labels/compile", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    client: "opti",
    brand: "zebra",
    layout: "template",
    printerDpi: 300,
    template: mergedTemplateConfig,
    labelData: piece,
    configuration,
  }),
});
const { zpl } = await res.json();
await invoke("send_zpl_tcp", { host: printerIp, zpl, port: 9100 });
```

---

## Metro layout (ERP built-in)

Use when ERP does **not** send an Opti template — fixed glass label layout only:

```json
{
  "client": "erp",
  "brand": "sato",
  "layout": "metro",
  "printerDpi": 300,
  "widthMm": 100,
  "heightMm": 150,
  "fields": {
    "orderNo": "38551 /2",
    "custOrderNo": "DS-002",
    "jobDescription": "ACTIVE ALUMINIUM WINDOWS",
    "dimensions": "1120 X 980",
    "glassSpec": "4.00mm Clear Float",
    "deliveryDate": "Tue 1/9",
    "sqm": "2.20",
    "lineRef": "5 / 2",
    "route": "Route 4",
    "weightKg": "10.98",
    "processNotes": ["Flat Polish"],
    "barcodeValue": "L-302925-1"
  }
}
```

`layout: "metro"` always uses this built-in layout and **ignores** any `template` on the same request. Opti does not use metro.

---

## Batch printing

`POST /api/labels/compile-batch`

- **Template:** one `template`, array `labels` of `labelData` objects.
- **Metro:** array `metroLabels` of `fields` objects.

Returned `payload` / `zpl` is multiple jobs concatenated — send once to the printer for a multi-label job. Metro batch also accepts `labels[]` (same objects as `fields`).

---

## Optional: send from the server

Only use when the **IIS server** is on the same network as printers.

1. Set `LabelPrint:AllowTcpSend: true` in `appsettings.json`.
2. `POST /api/labels/send` with `host` = printer IP, `port` = `9100`.

**Recommended:** clients compile via `/compile` and print via Opti `send_zpl_tcp` or ERP `TcpClient` — no printer firewall rules on the server.

---

## Printer brands

| `brand` | Language generated | Printer setting |
|---------|--------------------|-----------------|
| `zebra` | ZPL | Native ZPL |
| `honeywell` | ZPL | ZSim / ZPL emulation ON |
| `citizen` | ZPL | Zebra ZPL emulation ON |
| `sato` | ZPL | SZPL / ZPL emulation ON |
| `sato-sbpl` | SBPL | Native SBPL |
| `tsc` | TSPL | Native TSPL |
| `godex` | EZPL | Native EZPL |
| `datamax` | DPL | Native DPL |
| `epl` | EPL2 | Older Eltron / Zebra EPL |

Opti / Opti-TV send `brand` on compile. The same template is compiled, then converted to that printer language. All still go to the printer on TCP 9100. TSC / Godex / Datamax / SATO SBPL need the Label Print Service (in-app capture print is ZPL only).

---

## Troubleshooting

### 502.5 / “Failed to load CoreCLR” / app won’t start

- Install **.NET 8 Hosting Bundle** (not only SDK).
- Run `iisreset`.
- Check `C:\inetpub\LabelPrintService\logs\stdout_*.log`.

### HTTP 500 on compile

- Template must have `sections.*.fields` or root `fields` array.
- Very large templates (embedded logo base64) are normal; ensure request size limit in IIS is enough (default is usually fine for single labels).

### Empty or missing fields on label

- Confirm `labelData` keys match `{{tokens}}` or `fieldKey`.
- For note mappings, include `fieldMappings` on the template and `noteN` objects on `labelData`.
- See `samples/lbl004-piece.json` for a working example.

### `AllowTcpSend` / send fails

- Printer IP reachable from **server** (ping / telnet port 9100).
- Windows firewall on server allows outbound 9100.
- Honeywell/SATO: ZPL emulation enabled on device.

### Local `dotnet run` — wrong .NET version

Machine has .NET 9/10 but not 8: project uses `RollForward: LatestMajor`, or set:

```powershell
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
```

### CORS

API allows any origin (for browser tools). Production LAN clients (Opti desktop, ERP server) are not browsers — CORS does not apply.

---

## Quick checklist

| Step | Command / action |
|------|------------------|
| Build | `dotnet build` in `src/Spil.LabelPrint.Service` |
| Local run | `dotnet run` → http://localhost:5088/api/health |
| Publish | `dotnet publish -c Release -o C:\inetpub\LabelPrintService` |
| IIS pool | **No Managed Code**, Integrated |
| Hosting | .NET 8 **Hosting Bundle** + `iisreset` |
| Test compile | POST `/api/labels/compile` with LBL_004 + piece JSON |
| Print | Send returned `zpl` to printer **TCP 9100** from client |
