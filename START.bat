@echo off
setlocal EnableDelayedExpansion
title RagnaController Build ^& Publish
color 0B
cd /d "%~dp0"

echo.
echo  ================================================================
echo   RagnaController  --  Build ^& Publish
echo  ================================================================
echo.

:: ── Version aus .csproj lesen (automatisch, niemals hardcoden) ──────────
set "PROJ="
for /r "%~dp0src" %%f in (*.csproj) do set "PROJ=%%f"

if not defined PROJ (
    echo  [!] Keine .csproj im Ordner 'src' gefunden!
    pause & exit /b 1
)

set "APP_VERSION=?.?.?"
for /f "usebackq tokens=1" %%V in (`powershell -NoProfile -Command ^
    "([xml](Get-Content '%PROJ%')).Project.PropertyGroup.Version"`) do (
    set "APP_VERSION=%%V"
)

echo   Version: v%APP_VERSION%
echo   Projekt: %PROJ%
echo.

:: ── Admin-Check ──────────────────────────────────────────────────────────
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo  [!] Bitte Rechtsklick ^> Als Administrator ausfuehren!
    pause & exit /b 1
)

:: ── .NET Check ───────────────────────────────────────────────────────────
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo  [!] dotnet.exe nicht gefunden.
    echo      https://dotnet.microsoft.com/download/dotnet/8.0
    pause & exit /b 1
)

:: ── Laufende Instanz beenden ─────────────────────────────────────────────
echo  [*] Pruefe ob RagnaController bereits laeuft...
tasklist /FI "IMAGENAME eq RagnaController.exe" 2>nul | find /I "RagnaController.exe" >nul
if %errorLevel% equ 0 (
    echo  [*] Beende laufende RagnaController-Instanz...
    taskkill /F /IM RagnaController.exe >nul 2>&1
    timeout /T 2 /NOBREAK >nul
    echo  [OK] Prozess beendet.
) else (
    echo  [OK] Kein laufender Prozess gefunden.
)
echo.

:: ── publish\-Ordner leeren ───────────────────────────────────────────────
set "PUBDIR=%~dp0publish"
if exist "%PUBDIR%" (
    echo  [*] Leere publish\-Ordner...
    rd /s /q "%PUBDIR%" >nul 2>&1
    echo  [OK] publish\ bereinigt.
) else (
    echo  [OK] publish\ nicht vorhanden - wird neu erstellt.
)
echo.

:: ── Build ────────────────────────────────────────────────────────────────
echo  ========================================
echo   Building (Release)
echo  ========================================
dotnet build "%PROJ%" -c Release --nologo -v quiet
if %errorLevel% neq 0 (
    echo.
    echo  [!] Build fehlgeschlagen - siehe Fehler oben.
    pause & exit /b 1
)
echo  [OK] Build succeeded
echo.

:: ── Publish (triggert csproj PostBuild: BATs kopieren + ZIP erstellen) ───
echo  ========================================
echo   Publishing
echo  ========================================
echo  [*] Starte Publish...
echo      (dauert ca. 30 Sekunden)
echo.

dotnet publish "%PROJ%" -c Release --nologo --output "%PUBDIR%"

if %errorLevel% neq 0 (
    echo.
    echo  [!] Publish fehlgeschlagen - siehe Fehler oben.
    pause & exit /b 1
)

:: ── Ergebnis ─────────────────────────────────────────────────────────────
echo.
echo  ================================================================
echo  [OK] Fertig! v%APP_VERSION%
echo.
echo   EXE:  %~dp0publish\RagnaController.exe
echo   ZIP:  %~dp0RagnaController_v%APP_VERSION%.zip
echo  ================================================================
echo.

:: ── App direkt starten? ──────────────────────────────────────────────────
set /p LAUNCH="  App jetzt starten? (j/n): "
if /i "%LAUNCH%"=="j" (
    start "" "%~dp0publish\RagnaController.exe"
)

pause
