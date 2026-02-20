@echo off
setlocal EnableDelayedExpansion

title RagnaController Setup

:: ── Check for admin rights ────────────────────────────────────────────────────
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo.
    echo  [RagnaController Setup]
    echo  Requesting administrator privileges...
    echo.
    PowerShell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: ── Check PowerShell version ──────────────────────────────────────────────────
for /f "tokens=*" %%i in ('PowerShell -Command "$PSVersionTable.PSVersion.Major" 2^>nul') do set PS_VER=%%i

if "!PS_VER!" LSS "5" (
    echo.
    echo  ERROR: PowerShell 5.1 or higher is required.
    echo  Please update Windows PowerShell and try again.
    echo.
    pause
    exit /b 1
)

:: ── Run the PowerShell installer ──────────────────────────────────────────────
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"

:: ── Exit code handling ────────────────────────────────────────────────────────
if %errorLevel% NEQ 0 (
    echo.
    echo  Setup encountered an error. Check the log file in %%TEMP%%\RagnaController_Setup\setup.log
    echo.
    pause
)

endlocal
