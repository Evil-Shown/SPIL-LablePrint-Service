SPIL Label Print Service — shop PC (no Git / no Visual Studio)

1. Copy this WHOLE folder to the shop PC (USB, network share, zip).
   Example: C:\SPIL\LabelPrintService\

2. Double-click  Install-LabelPrintService.bat
   Click Yes on the Administrator prompt.
   Wait until it says installed and running.

   If Opti on OTHER PCs must call this machine, double-click
   Install-LabelPrintService-ListenLan.bat instead.

   Do NOT open the .ps1 files. They will open in VS Code / Notepad and will not install.

3. On this PC, open in a browser:
   http://localhost:5088/api/health
   You should see "ok": true

4. In Opti (Configuration → Labels):
   - Enable direct print = ON
   - Printer IP = the printer (example 192.168.1.50)
   - Printer brand = Zebra / TSC / etc.
   - Use Label Print Service = ON
   - Service URL = http://localhost:5088

5. Print from Opti as usual. Users do not open a window after this.

Notes
- This folder already includes .NET. You do NOT install Visual Studio or Git.
- If Windows Defender blocks the .exe, allow it (it is your own build).
- The service name is SpilLabelPrintService. It restarts itself if it crashes.
- To test without installing a service, you can still use Start-LabelPrintService.bat
  (keep that window open). Prefer the Windows Service on real shop PCs.
- If Opti is on a DIFFERENT PC, use ListenLan and set Service URL to
  http://THIS-PC-LAN-IP:5088
- To stop/remove the service: double-click Uninstall-LabelPrintService.bat
  This does not delete the folder.
- If health fails after install, Event Viewer → Windows Logs → Application
  (source SpilLabelPrintService).
