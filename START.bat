@echo off
setlocal enabledelayedexpansion
title RagnaController Build ^& Publish

:: Ensure we are in the correct directory
cd /d "%~dp0"

echo ==========================================================
echo        RAGNA CONTROLLER - BUILD ^& PUBLISH TOOL
echo ==========================================================
echo.

:: 0. Extract Version from csproj
set APP_VER=1.0.0
if exist "src\RagnaController\RagnaController.csproj" (
    for /f "tokens=3 delims=<>" %%a in ('findstr "<Version>" "src\RagnaController\RagnaController.csproj"') do set APP_VER=%%a
)
echo [*] Detected Version: v!APP_VER!
echo.

:: 1. Check if .NET 8 SDK is installed
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] ERROR: 'dotnet' command not found.
    echo RagnaController requires the .NET 8 SDK to compile.
    echo.
    echo Press any key to automatically download and install the .NET 8 SDK...
    pause >nul
    
    echo.
    echo [*] Downloading official .NET 8 SDK from Microsoft...
    :: Enforce TLS 1.2 for secure download
    powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://download.visualstudio.microsoft.com/download/pr/4d4fa734-7a91-4c74-9b2f-7f7cb63a7f72/1d2b45ba1a6db345a5572bb46522c6c0/dotnet-sdk-8.0.203-win-x64.exe' -OutFile 'dotnet-sdk-8.exe'"
    
    if exist "dotnet-sdk-8.exe" (
        echo [OK] Download complete. Starting Installer...
        start /wait dotnet-sdk-8.exe
        
        :: Cleanup installer
        del /f /q "dotnet-sdk-8.exe"
        
        echo.
        echo Please restart this script after the installation finishes.
        pause
        exit /b 1
    ) else (
        echo [!] Download failed. Please install manually:
        echo https://dotnet.microsoft.com/download/dotnet/8.0
        pause
        exit /b 1
    )
)

:: 2. Close running instances to prevent file lock errors during build
echo [*] Checking for running RagnaController processes...
taskkill /F /IM RagnaController.exe >nul 2>&1
if %errorLevel% equ 0 (
    echo [OK] Closed running instances.
) else (
    echo [OK] No running instances found.
)
echo.

:: 3. Show Menu
echo Please select a target platform:
echo ----------------------------------------------------------
echo [1] Windows        -- Framework-dependent
echo     Small (~2 MB), requires .NET 8 installed on target PC
echo.
echo [2] Windows        -- Self-Contained
echo     Large (~140 MB), .NET 8 embedded, zero setup for user
echo.
echo [3] Steam Deck     -- Self-Contained via Proton
echo     Windows EXE, runs flawlessly via "Add a Non-Steam Game"
echo.
echo [4] Build all three at once
echo ----------------------------------------------------------
set /p buildChoice="Selection (1/2/3/4): "

if "%buildChoice%"=="" set buildChoice=1

echo.
echo [*] Cleaning old build files...
dotnet clean src\RagnaController\RagnaController.csproj -c Release >nul 2>&1
if exist "publish" rmdir /s /q "publish"

echo.
echo ==========================================================
echo  Building (Release) - v!APP_VER!
echo ==========================================================
echo.

if "%buildChoice%"=="1" goto buildWinFD
if "%buildChoice%"=="2" goto buildWinSC
if "%buildChoice%"=="3" goto buildSteamDeck
if "%buildChoice%"=="4" goto buildAll
goto buildWinFD

:buildWinFD
echo Building Windows (Framework-dependent)...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\Windows_FrameworkDependent
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\Windows_FrameworkDependent" "Win_Light"
goto success

:buildWinSC
echo Building Windows (Self-Contained)...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\Windows_SelfContained
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\Windows_SelfContained" "Win_Standalone"
goto success

:buildSteamDeck
echo Building Steam Deck / Proton (Self-Contained)...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\SteamDeck_Proton
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\SteamDeck_Proton" "SteamDeck"
goto success

:buildAll
echo 1/3: Building Windows Framework-dependent...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\Windows_FrameworkDependent
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\Windows_FrameworkDependent" "Win_Light"

echo.
echo 2/3: Building Windows Self-Contained...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\Windows_SelfContained
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\Windows_SelfContained" "Win_Standalone"

echo.
echo 3/3: Building Steam Deck (Proton) Self-Contained...
dotnet publish src\RagnaController\RagnaController.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\SteamDeck_Proton
if %errorLevel% neq 0 goto error
call :finalizeRelease "publish\SteamDeck_Proton" "SteamDeck"
goto success

:: ---------------------------------------------------------
:: Subroutine: Copy docs and create ZIP archive
:: ---------------------------------------------------------
:finalizeRelease
set "targetDir=%~1"
set "zipSuffix=%~2"
echo [*] Copying documentation...
if exist "README.md" copy /y "README.md" "%targetDir%\" >nul
if exist "LICENSE" copy /y "LICENSE" "%targetDir%\" >nul
if exist "docs" xcopy /s /i /y "docs" "%targetDir%\docs" >nul

echo [*] Compressing to ZIP...
powershell -NoProfile -Command "Compress-Archive -Path '%targetDir%\*' -DestinationPath 'publish\RagnaController_v!APP_VER!_%zipSuffix%.zip' -Force"
exit /b 0

:: ---------------------------------------------------------
:: Error / Success Handling
:: ---------------------------------------------------------
:error
echo.
echo [!] Build failed. Please check the error messages above.
pause
exit /b 1

:success
echo.
echo ==========================================================
echo  [OK] Build completed successfully!
echo  The ready-to-use ZIP files are located in the "publish" folder.
echo ==========================================================
echo.
echo Do you want to open the publish folder and run the app? (Y/N)
set /p runChoice="Selection: "

if /I "%runChoice%"=="Y" (
    start "" "publish"
    if exist "publish\Windows_FrameworkDependent\RagnaController.exe" (
        start "" "publish\Windows_FrameworkDependent\RagnaController.exe"
    ) else if exist "publish\Windows_SelfContained\RagnaController.exe" (
        start "" "publish\Windows_SelfContained\RagnaController.exe"
    )
)

exit /b 0