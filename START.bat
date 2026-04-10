@echo off
setlocal EnableDelayedExpansion
title RagnaController Build ^& Publish
color 0B

:checkPrivileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "cmd.exe", "/c ""%~f0"" ELEV", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /b
)

cd /d "%~dp0"

echo.
echo  ================================================================
echo   RagnaController  --  Build ^& Publish (ADMIN)
echo  ================================================================
echo.

set "PROJ="
for /r "%~dp0src" %%f in (*.csproj) do set "PROJ=%%f"

if not defined PROJ (
    echo  [!] No .csproj found in 'src' folder!
    pause & exit /b 1
)

set "APP_VERSION=?.?.?"
for /f "usebackq tokens=1" %%V in (`powershell -NoProfile -Command ^
    "([xml](Get-Content '%PROJ%')).Project.PropertyGroup.Version"`) do (
    set "APP_VERSION=%%V"
)

echo   Version: v%APP_VERSION%
echo   Project: %PROJ%
echo.

where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo  [!] dotnet.exe not found.
    echo      https://dotnet.microsoft.com/download/dotnet/8.0
    pause & exit /b 1
)

echo  [*] Checking if RagnaController is already running...
tasklist /FI "IMAGENAME eq RagnaController.exe" 2>nul | find /I "RagnaController.exe" >nul
if %errorLevel% equ 0 (
    echo  [*] Terminating running RagnaController instance...
    taskkill /F /IM RagnaController.exe >nul 2>&1
    timeout /T 2 /NOBREAK >nul
    echo  [OK] Process terminated.
) else (
    echo  [OK] No running process found.
)
echo.

echo  ================================================================
echo   Select target platform:
echo  ================================================================
echo.
echo   [1]  Windows        -- Framework-dependent
echo          Small (~2 MB), requires .NET 8 installed
echo.
echo   [2]  Windows        -- Self-Contained
echo          Large (~75 MB), .NET 8 bundled, no install needed
echo.
echo   [3]  Steam Deck     -- Self-Contained via Proton
echo          Windows EXE with bundled .NET 8, runs via Proton
echo.
echo   [4]  Build all three
echo.
set /p BUILD_CHOICE="  Choice (1/2/3/4): "

echo.
if "%BUILD_CHOICE%"=="1" goto :build_fd
if "%BUILD_CHOICE%"=="2" goto :build_sc_win
if "%BUILD_CHOICE%"=="3" goto :build_sc_deck
if "%BUILD_CHOICE%"=="4" goto :build_all
echo  [!] Invalid choice.
pause & exit /b 1

REM ============================================================
REM  OPTION 1 -- Windows Framework-dependent
REM ============================================================
:build_fd
call :do_build
set "PUBDIR=%~dp0publish\win-framework"
echo  ========================================
echo   Publish: Windows Framework-dependent
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained false ^
    /p:PublishSingleFile=true --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Publish failed. & pause & exit /b 1 )
set "ZIPOUT=%~dp0RagnaController_v%APP_VERSION%_win-framework.zip"
call :make_zip "%PUBDIR%" "%ZIPOUT%"
goto :done_single

REM ============================================================
REM  OPTION 2 -- Windows Self-Contained
REM ============================================================
:build_sc_win
call :do_build
set "PUBDIR=%~dp0publish\win-selfcontained"
echo  ========================================
echo   Publish: Windows Self-Contained
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Publish failed. & pause & exit /b 1 )
set "ZIPOUT=%~dp0RagnaController_v%APP_VERSION%_win-selfcontained.zip"
call :make_zip "%PUBDIR%" "%ZIPOUT%"
goto :done_single

REM ============================================================
REM  OPTION 3 -- Steam Deck via Proton
REM ============================================================
:build_sc_deck
call :do_build
set "PUBDIR=%~dp0publish\steamdeck-proton"
echo  ========================================
echo   Publish: Steam Deck (Proton, win-x64)
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Publish failed. & pause & exit /b 1 )

call :write_deck_readme "%PUBDIR%\SteamDeck_SETUP.txt"

set "ZIPOUT=%~dp0RagnaController_v%APP_VERSION%_steamdeck-proton.zip"
call :make_zip "%PUBDIR%" "%ZIPOUT%"
goto :done_single

REM ============================================================
REM  OPTION 4 -- Build all three
REM ============================================================
:build_all
call :do_build

REM -- [1/3] Windows Framework-dependent --
set "PUBDIR=%~dp0publish\win-framework"
echo  ========================================
echo   [1/3] Windows Framework-dependent
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained false ^
    /p:PublishSingleFile=true --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Failed [1/3]. & pause & exit /b 1 )
set "Z1=%~dp0RagnaController_v%APP_VERSION%_win-framework.zip"
call :make_zip "%PUBDIR%" "%Z1%"
echo  [OK] win-framework done.
echo.

REM -- [2/3] Windows Self-Contained --
set "PUBDIR=%~dp0publish\win-selfcontained"
echo  ========================================
echo   [2/3] Windows Self-Contained
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Failed [2/3]. & pause & exit /b 1 )
set "Z2=%~dp0RagnaController_v%APP_VERSION%_win-selfcontained.zip"
call :make_zip "%PUBDIR%" "%Z2%"
echo  [OK] win-selfcontained done.
echo.

REM -- [3/3] Steam Deck via Proton --
set "PUBDIR=%~dp0publish\steamdeck-proton"
echo  ========================================
echo   [3/3] Steam Deck (Proton, win-x64)
echo  ========================================
dotnet publish "%PROJ%" -c Release --nologo -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    --output "%PUBDIR%"
if %errorLevel% neq 0 ( echo  [!] Failed [3/3]. & pause & exit /b 1 )
call :write_deck_readme "%PUBDIR%\SteamDeck_SETUP.txt"
set "Z3=%~dp0RagnaController_v%APP_VERSION%_steamdeck-proton.zip"
call :make_zip "%PUBDIR%" "%Z3%"
echo  [OK] steamdeck-proton done.

echo.
echo  ================================================================
echo  [OK] All 3 targets complete!  v%APP_VERSION%
echo.
echo   [1] %Z1%
echo   [2] %Z2%
echo   [3] %Z3%
echo  ================================================================
echo.
set /p LAUNCH="  Launch Windows app now? (y/n): "
if /i "%LAUNCH%"=="y" (
    start "" "%~dp0publish\win-framework\RagnaController.exe"
)
pause
exit /b 0

REM ============================================================
REM  HELPER: Single build pass
REM ============================================================
:do_build
echo  ========================================
echo   Cleaning old builds...
echo  ========================================
if exist "%~dp0publish" (
    rd /s /q "%~dp0publish" >nul 2>&1
    timeout /t 1 /nobreak >nul
)

REM Sicherheits-Check: Konnte der Ordner wirklich geloescht werden?
if exist "%~dp0publish" (
    echo.
    echo  [!] ERROR: Could not delete the 'publish' folder.
    echo      Make sure you do NOT have the folder open in Windows Explorer!
    echo.
    pause & exit /b 1
)

dotnet clean "%PROJ%" -c Release -v quiet >nul 2>&1

echo  ========================================
echo   Build (Release)
echo  ========================================
dotnet build "%PROJ%" -c Release --nologo -v quiet
if %errorLevel% neq 0 (
    echo  [!] Build failed.
    pause & exit /b 1
)
echo  [OK] Build succeeded.
echo.
goto :eof

REM ============================================================
REM  HELPER: Create ZIP (and copy docs/readme)
REM  %~1 = source folder (PUBDIR)   %~2 = ZIP path
REM ============================================================
:make_zip
echo  [*] Copying README, LICENSE and docs...
if exist "%~dp0README.md" copy /y "%~dp0README.md" "%~1\" >nul 2>&1
if exist "%~dp0LICENSE" copy /y "%~dp0LICENSE" "%~1\" >nul 2>&1
if exist "%~dp0docs" (
    mkdir "%~1\docs" >nul 2>&1
    xcopy /e /y /q "%~dp0docs\*" "%~1\docs\" >nul 2>&1
)

if exist "%~2" del "%~2"
powershell -NoProfile -Command ^
    "Compress-Archive -Path '%~1\*' -DestinationPath '%~2'"
echo  [ZIP] %~2
goto :eof

REM ============================================================
REM  HELPER: Write SteamDeck_SETUP.txt
REM  %~1 = target file path
REM ============================================================
:write_deck_readme
(
echo ================================================================
echo  RagnaController v%APP_VERSION% -- Steam Deck Setup via Proton
echo ================================================================
echo.
echo  This is a Windows application and runs via Proton.
echo  .NET 8 is bundled -- no separate install required.
echo.
echo  SETUP ON STEAM DECK
echo  -----------------------------------------------
echo  1. Copy the ZIP to your Steam Deck (e.g. via USB or SSH)
echo     Recommended path: /home/deck/Games/RagnaController/
echo.
echo  2. Open Steam in Desktop Mode
echo.
echo  3. "Add a Non-Steam Game to My Library"
echo     -^> "Browse" -^> select RagnaController.exe
echo.
echo  4. Library entry: right-click -^> Properties
echo     -^> Compatibility
echo     -^> Check "Force the use of a specific Steam Play compatibility tool"
echo     -^> Select Proton Experimental or Proton 9.x
echo.
echo  5. Optional: set launch options (Properties -^> General)
echo     WINEDLLOVERRIDES="xinput1_3=n,b" %%command%%
echo     (improves XInput controller detection under Proton)
echo.
echo  6. Launch the game -- RagnaController opens in Desktop Mode
echo.
echo  NOTES
echo  -----------------------------------------------
echo  - Controller input: XInput is supported by Proton.
echo    Configure the Steam Deck's own controller as "Xbox Controller"
echo    in Steam settings.
echo  - WPF rendering works correctly via Wine/Proton.
echo  - Audio (startup voice) works when Proton audio is active.
echo  - For SSH access on the Deck: Desktop Mode -^> Terminal
echo    sudo systemctl start sshd
echo.
echo  SUPPORT
echo  -----------------------------------------------
echo  GitHub: https://github.com/Patbeck85/RagnaController
echo.
echo ================================================================
) > "%~1"
echo  [OK] SteamDeck_SETUP.txt created.
goto :eof

REM ============================================================
:done_single
echo.
echo  ================================================================
echo  [OK] Done!  v%APP_VERSION%
echo.
echo   EXE: %PUBDIR%\RagnaController.exe
echo   ZIP: %ZIPOUT%
echo  ================================================================
echo.

echo %PUBDIR% | find "steamdeck" >nul
if %errorLevel% neq 0 (
    set /p LAUNCH="  Launch app now? (y/n): "
    if /i "!LAUNCH!"=="y" start "" "%PUBDIR%\RagnaController.exe"
) else (
    echo  [i] Steam Deck build -- launch via Proton on the Deck.
)

pause
exit /b 0