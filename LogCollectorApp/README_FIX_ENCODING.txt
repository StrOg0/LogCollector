The previous build-installer.ps1 contained Cyrillic text in UTF-8 without BOM.
Windows PowerShell 5.1 can parse such files incorrectly on Russian Windows.
This fixed script contains ASCII-only messages, so it works in Windows PowerShell 5.1 and PowerShell 7.

Run from the project folder:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1

Or double-click:
  build-installer.cmd
