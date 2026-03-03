@echo off
title .NET Runtime – Check

:: ── Prüfe ob .NET 8 ODER NEUER (Windows Desktop Runtime) installiert ist ──
:: WPF benötigt Microsoft.WindowsDesktop.App, NICHT Microsoft.NETCore.App!
set "FOUND="
for /f "tokens=1,2" %%A in ('dotnet --list-runtimes 2^>nul') do (
    if "%%A"=="Microsoft.WindowsDesktop.App" (
        for /f "tokens=1 delims=." %%M in ("%%B") do (
            if %%M GEQ 8 set "FOUND=1"
        )
    )
)

if defined FOUND (
    echo .NET Windows Desktop Runtime 8 oder neuer gefunden.
    timeout /t 2 /nobreak >nul
    exit
)

:: ── Nicht gefunden – herunterladen ────────────────────────────────────────
echo .NET 8 Windows Desktop Runtime nicht gefunden.
echo Wird heruntergeladen (~55 MB) ...
echo.
echo WICHTIG: Bitte "Windows Desktop Runtime" installieren, nicht nur die Base Runtime!
echo.

powershell -NoProfile -Command ^
  "Invoke-WebRequest -Uri 'https://aka.ms/dotnet-8-windowsdesktop-x64' -OutFile '%TEMP%\dotnet8desktop.exe' -UseBasicParsing; ^
   Start-Process -FilePath '%TEMP%\dotnet8desktop.exe' -Wait"

echo.
echo Fertig. Du kannst RagnaController.exe jetzt starten.
pause
