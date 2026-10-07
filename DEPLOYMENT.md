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
  * `Install-LabelPrintService.ps1` (Windows Service, auto-start)
  * `Uninstall-LabelPrintService.ps1`
  * `Start-LabelPrintService.bat` (optional test window)
  * `README.txt` (quick guide for shop technicians)
  * Required DLLs and runtime dependencies

---

## 2. Deploying to a Shop Floor PC

1. **Copy the Output**:
   Copy the entire `dist\LabelPrintService` folder to the target PC (via USB drive or network share):
   ```
   C:\SPIL\LabelPrintService\
   ```

2. **Install as a Windows Service** (always running, starts at boot).

   Open **PowerShell as Administrator** in `C:\SPIL\LabelPrintService`:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\Install-LabelPrintService.ps1
   ```

   Opti on other PCs must call this machine:

   ```powershell
   .\Install-LabelPrintService.ps1 -ListenLan
   ```

   Default URL: `http://127.0.0.1:5088` (localhost only, unless `-ListenLan`).

3. **Verify Health**:
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

## 4. Windows Service (shop PCs)

This is the supported way to run on user PCs. The process:

* Starts when Windows boots (no login, no `.bat`, no console window)
* Restarts automatically if it crashes
* Listens on `http://127.0.0.1:5088` unless you pass `-ListenLan`

```powershell
# From C:\SPIL\LabelPrintService in an elevated PowerShell
.\Install-LabelPrintService.ps1
```

Useful commands after install:

```powershell
Get-Service SpilLabelPrintService
Restart-Service SpilLabelPrintService
Invoke-RestMethod http://localhost:5088/api/health
```

To stop and unregister (files stay on disk):

```powershell
.\Uninstall-LabelPrintService.ps1
```

To test without installing a service, double-click `Start-LabelPrintService.bat` and keep that window open.

If health fails: Event Viewer → Windows Logs → Application → source **SpilLabelPrintService**.

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

