@echo off
setlocal

:: ── RagnaController Quick Launcher ────────────────────────────────────────────
:: Double-click this to start RagnaController.
:: If .NET 8 is missing, it opens the Setup automatically.

set "APP=%~dp0RagnaController.exe"
set "SETUP=%~dp0installer\Setup.bat"

if exist "%APP%" (
    :: Quick .NET check
    dotnet --list-runtimes 2>nul | findstr /C:"Microsoft.WindowsDesktop.App 8." >nul
    if errorlevel 1 (
        echo .NET 8 Runtime not found. Running setup...
        call "%SETUP%"
    ) else (
        start "" "%APP%"
    )
) else (
    echo RagnaController.exe not found.
    echo Please run the installer first or extract all files.
    pause
)

endlocal
