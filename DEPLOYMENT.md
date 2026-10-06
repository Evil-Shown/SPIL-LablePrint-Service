# Label Print Service — Build & Deployment Guide

This guide explains how to build, package, and deploy the **SPIL Label Print Service** (.NET 8 Web API) on shop floor PCs or servers.

---

## 1. Quick Release / Packaging

To create a standalone package that does **NOT** require .NET, Visual Studio, or Git on the destination machine:

```powershell
cd "D:\Coding\SPIL LABS\Repos\Lable Print Service"
.\pack-for-shop-pc.ps1
```

### What this produces:
* **Output Folder**: `dist\LabelPrintService\`
* **Self-Contained Executable**: Embeds the full .NET runtime inside the build.
* **Included Files**:
  * `Spil.LabelPrint.Service.exe`
  * `Start-LabelPrintService.bat` (one-click runner)
  * `README.txt` (quick guide for shop technicians)
  * Required DLLs and runtime dependencies

---

## 2. Deploying to a Shop Floor PC

1. **Copy the Output**:
   Copy the entire `dist\LabelPrintService` folder to the target PC (via USB drive or network share):
   ```
   C:\SPIL\LabelPrintService\
   ```

2. **Configure Windows Firewall** *(Only required if other machines/devices connect to this PC)*:
   Open PowerShell as Administrator:
   ```powershell
   New-NetFirewallRule -DisplayName "SPIL Label Print Service" -Direction Inbound -Protocol TCP -LocalPort 5088 -Action Allow
   ```

3. **Start the Service**:
   Double-click `Start-LabelPrintService.bat`.
   * Keep the console window open while printing.
   * Default URL: `http://0.0.0.0:5088`

4. **Verify Health**:
   Open a browser on that PC or over the network:
   * **Health check**: [http://localhost:5088/api/health](http://localhost:5088/api/health) (returns `{"ok": true}`)
   * **Swagger UI**: [http://localhost:5088/swagger](http://localhost:5088/swagger)

---

## 3. Configuring Opti / ERP / Label Crafter

In Opti (**Configuration → Labels**):
* **Direct Print**: `ON`
* **Printer IP**: IP address of the target printer (e.g. `192.168.1.50:9100`)
* **Printer Brand**: Zebra / TSC / etc.
* **Use Label Print Service**: `ON`
* **Service URL**: `http://localhost:5088` (or `http://<SHOP-PC-IP>:5088` if running on another machine)

---

## 4. Optional: Run as Windows Service (Auto-Start on Boot)

If you prefer the service to run silently in the background without needing a user logged in:

Open PowerShell as Administrator:
```powershell
sc.exe create "SpilLabelPrintService" binPath= "C:\SPIL\LabelPrintService\Spil.LabelPrint.Service.exe" start= auto
sc.exe start "SpilLabelPrintService"
```

To stop or remove the service later:
```powershell
sc.exe stop "SpilLabelPrintService"
sc.exe delete "SpilLabelPrintService"
```

---

## 5. Manual Build Commands (CI / CLI Reference)

```powershell
# Debug run locally:
dotnet run --project src\Spil.LabelPrint.Service

# Framework-dependent release:
dotnet publish src\Spil.LabelPrint.Service -c Release -o bin\Release\publish

# Fully self-contained Windows x64 release:
dotnet publish src\Spil.LabelPrint.Service -c Release -r win-x64 --self-contained true -o dist\LabelPrintService
```

