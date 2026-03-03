@echo off
setlocal EnableDelayedExpansion
title DS4Windows Downloader
color 0B

echo.
echo  ================================================================
echo   DS4Windows Downloader fuer RagnaController
echo  ================================================================
echo.
echo  DS4Windows ermoeglicht die Verwendung von PlayStation-Controllern
echo  (DualShock 4 / DualSense) als Xbox-Controller.
echo.
echo  RagnaController benoetigt einen Xbox-kompatiblen Controller.
echo  Mit DS4Windows funktionieren auch PS4/PS5-Controller.
echo.
echo  ================================================================
echo.

:: ── PowerShell verfuegbar? ────────────────────────────────────────────────
where powershell >nul 2>&1
if %errorLevel% neq 0 (
    echo  [!] PowerShell nicht gefunden.
    echo      Bitte DS4Windows manuell herunterladen:
    echo      https://github.com/Ryochan7/DS4Windows/releases/latest
    echo.
    pause & exit /b 1
)

echo  [*] Suche aktuellste DS4Windows-Version via GitHub API...
echo.

:: ── Aktuellste Release-Info von GitHub holen ─────────────────────────────
set "API_URL=https://api.github.com/repos/Ryochan7/DS4Windows/releases/latest"
set "TMP_JSON=%TEMP%\ds4w_release.json"

powershell -NoProfile -Command ^
  "try { Invoke-RestMethod -Uri '%API_URL%' -UseBasicParsing | ConvertTo-Json -Depth 5 | Out-File '%TMP_JSON%' -Encoding utf8; exit 0 } catch { exit 1 }"

if %errorLevel% neq 0 (
    echo  [!] GitHub API nicht erreichbar.
    echo      Bitte manuell herunterladen:
    echo      https://github.com/Ryochan7/DS4Windows/releases/latest
    echo.
    start "" "https://github.com/Ryochan7/DS4Windows/releases/latest"
    pause & exit /b 1
)

:: ── Download-URL aus JSON extrahieren (erste .zip Asset) ─────────────────
set "DOWNLOAD_URL="
set "VERSION="

for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command ^
  "$j = Get-Content '%TMP_JSON%' -Raw | ConvertFrom-Json; ^
   $v = $j.tag_name; ^
   $url = ($j.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1).browser_download_url; ^
   Write-Output \"$v|$url\""`) do (
    for /f "tokens=1,2 delims=|" %%B in ("%%A") do (
        set "VERSION=%%B"
        set "DOWNLOAD_URL=%%C"
    )
)

if not defined DOWNLOAD_URL (
    echo  [!] Download-URL nicht gefunden.
    echo      Bitte manuell herunterladen:
    echo      https://github.com/Ryochan7/DS4Windows/releases/latest
    echo.
    start "" "https://github.com/Ryochan7/DS4Windows/releases/latest"
    pause & exit /b 1
)

echo  [OK] Gefunden: DS4Windows %VERSION%
echo       URL: %DOWNLOAD_URL%
echo.

:: ── Ziel-Ordner ───────────────────────────────────────────────────────────
set "DEST_DIR=%~dp0DS4Windows"
set "ZIP_FILE=%TEMP%\DS4Windows_%VERSION%.zip"

echo  [*] Lade herunter nach: %DEST_DIR%
echo.

if not exist "%DEST_DIR%" mkdir "%DEST_DIR%"

:: ── Download ──────────────────────────────────────────────────────────────
powershell -NoProfile -Command ^
  "try { ^
     $ProgressPreference = 'SilentlyContinue'; ^
     Invoke-WebRequest -Uri '%DOWNLOAD_URL%' -OutFile '%ZIP_FILE%' -UseBasicParsing; ^
     exit 0 ^
   } catch { ^
     Write-Host \"[!] Download fehlgeschlagen: $($_.Exception.Message)\"; ^
     exit 1 ^
   }"

if %errorLevel% neq 0 (
    echo.
    echo  [!] Download fehlgeschlagen.
    echo      Bitte manuell herunterladen:
    echo      %DOWNLOAD_URL%
    echo.
    start "" "https://github.com/Ryochan7/DS4Windows/releases/latest"
    pause & exit /b 1
)

echo  [OK] Download abgeschlossen.
echo.
echo  [*] Entpacke nach %DEST_DIR%...

:: ── Entpacken ─────────────────────────────────────────────────────────────
powershell -NoProfile -Command ^
  "Expand-Archive -Path '%ZIP_FILE%' -DestinationPath '%DEST_DIR%' -Force"

if %errorLevel% neq 0 (
    echo  [!] Entpacken fehlgeschlagen.
    echo      ZIP liegt hier: %ZIP_FILE%
    pause & exit /b 1
)

del "%ZIP_FILE%" >nul 2>&1
del "%TMP_JSON%"  >nul 2>&1

echo  [OK] DS4Windows %VERSION% wurde entpackt nach:
echo       %DEST_DIR%
echo.
echo  ================================================================
echo   NAECHSTE SCHRITTE:
echo  ================================================================
echo.
echo   1. Oeffne den Ordner DS4Windows\
echo   2. Starte DS4Windows.exe (als Administrator empfohlen)
echo   3. Verbinde deinen PS4/PS5-Controller per USB oder Bluetooth
echo   4. DS4Windows emuliert einen Xbox-Controller
echo   5. Starte danach RagnaController.exe
echo.
echo  ================================================================
echo.

:: ── Ordner oeffnen ────────────────────────────────────────────────────────
set /p OPEN="  DS4Windows-Ordner jetzt oeffnen? (j/n): "
if /i "%OPEN%"=="j" (
    start "" "%DEST_DIR%"
)

echo.
echo  Fertig! Viel Spass mit RagnaController.
echo.
pause
