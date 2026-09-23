SPIL Label Print Service — shop PC (no Git / no Visual Studio)

1. Copy this WHOLE folder to the shop PC (USB, network share, zip).
   Example: C:\SPIL\LabelPrintService\

2. Double-click Start-LabelPrintService.bat
   Keep that window open.

3. On the same PC, open in a browser:
   http://localhost:5088/api/health
   You should see "ok": true

4. In Opti (Configuration → Labels):
   - Enable direct print = ON
   - Printer IP = the printer (example 192.168.1.50)
   - Printer brand = Zebra / TSC / etc.
   - Use Label Print Service = ON
   - Service URL = http://localhost:5088

5. Print from Opti as usual.

Notes
- This folder already includes .NET. You do NOT install Visual Studio or Git.
- If Windows Defender blocks the .exe, allow it (it is your own build).
- If Opti is on a DIFFERENT PC, set Service URL to http://THIS-PC-LAN-IP:5088
  and allow port 5088 in Windows Firewall on this PC.
- To stop: close the black window or press Ctrl+C.

Windows Firewall (only if other PCs must reach this service):
  New-NetFirewallRule -DisplayName "Label Print Service" -Direction Inbound -Protocol TCP -LocalPort 5088 -Action Allow
