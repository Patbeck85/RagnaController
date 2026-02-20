#Requires -Version 5.1
<#
.SYNOPSIS
    RagnaController Installer / Launcher
    Checks all prerequisites, downloads missing components and starts the app.

.DESCRIPTION
    Checks for:
      - Windows 10 / 11
      - .NET 8 Desktop Runtime
      - VC++ Redistributable (for SharpDX)
      - Xbox controller driver (XInput)
    Downloads and installs anything that is missing (with user consent).
    Then launches RagnaController.exe.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Config ─────────────────────────────────────────────────────────────────────

$AppName        = "RagnaController"
$AppVersion     = "1.0.0"
$AppExe         = Join-Path $PSScriptRoot "..\app\RagnaController.exe"
$TempDir        = Join-Path $env:TEMP "RagnaController_Setup"
$LogFile        = Join-Path $TempDir "setup.log"

$DotNetVersion  = "8.0"
$DotNetUrl      = "https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe"
$DotNetDesktop  = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

# ── Helpers ────────────────────────────────────────────────────────────────────

function Write-Header {
    Clear-Host
    Write-Host ""
    Write-Host "  ██████╗  █████╗  ██████╗ ███╗  ██╗ █████╗  " -ForegroundColor Cyan
    Write-Host "  ██╔══██╗██╔══██╗██╔════╝ ████╗ ██║██╔══██╗ " -ForegroundColor Cyan
    Write-Host "  ██████╔╝███████║██║  ███╗██╔██╗██║███████║ " -ForegroundColor Cyan
    Write-Host "  ██╔══██╗██╔══██║██║   ██║██║╚████║██╔══██║ " -ForegroundColor Cyan
    Write-Host "  ██║  ██║██║  ██║╚██████╔╝██║ ╚███║██║  ██║ " -ForegroundColor Cyan
    Write-Host "  ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚══╝╚═╝  ╚═╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Controller  ──  Setup & Launcher  ──  v$AppVersion" -ForegroundColor DarkCyan
    Write-Host "  ─────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ""
}

function Write-Step { param([string]$Icon, [string]$Msg, [ConsoleColor]$Color = "White")
    Write-Host "  $Icon  $Msg" -ForegroundColor $Color
}

function Write-Ok    { param([string]$Msg) Write-Step "✔" $Msg Green }
function Write-Warn  { param([string]$Msg) Write-Step "⚠" $Msg Yellow }
function Write-Info  { param([string]$Msg) Write-Step "→" $Msg Cyan }
function Write-Fail  { param([string]$Msg) Write-Step "✖" $Msg Red }

function Write-Log { param([string]$Msg)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $LogFile -Value "[$timestamp] $Msg" -ErrorAction SilentlyContinue
}

function Prompt-Continue { param([string]$Question)
    Write-Host ""
    Write-Host "  $Question" -ForegroundColor Yellow -NoNewline
    $answer = Read-Host " [Y/n]"
    return ($answer -eq "" -or $answer -match "^[Yy]")
}

function Download-File {
    param([string]$Url, [string]$Destination, [string]$Label)

    Write-Info "Downloading $Label ..."
    Write-Log  "Downloading $Label from $Url"

    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add("User-Agent", "RagnaController-Setup/$AppVersion")

    $progress = 0
    $wc.DownloadProgressChanged += {
        $pct = $_.ProgressPercentage
        if ($pct -ne $progress) {
            $progress = $pct
            Write-Progress -Activity "Downloading $Label" -Status "$pct%" -PercentComplete $pct
        }
    }

    try {
        $wc.DownloadFileTaskAsync($Url, $Destination).Wait()
        Write-Progress -Activity "Downloading $Label" -Completed
        Write-Ok "$Label downloaded"
        return $true
    }
    catch {
        Write-Fail "Download failed: $_"
        Write-Log  "Download failed: $_"
        return $false
    }
}

# ── Checks ─────────────────────────────────────────────────────────────────────

function Test-WindowsVersion {
    $os = [System.Environment]::OSVersion.Version
    $build = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion").CurrentBuildNumber -as [int]

    Write-Log "Windows Build: $build"

    if ($build -ge 19041) {  # Windows 10 2004+
        Write-Ok "Windows $($os.Major).$($os.Minor) (Build $build) — supported"
        return $true
    }
    else {
        Write-Warn "Windows version (Build $build) may not be fully supported. Recommended: Windows 10 20H1 or later."
        return $true  # Let them try anyway
    }
}

function Test-DotNet8 {
    Write-Log "Checking .NET 8 Desktop Runtime..."

    # Check via dotnet --list-runtimes
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes -match "Microsoft\.WindowsDesktop\.App 8\.") {
            $version = ($runtimes | Where-Object { $_ -match "Microsoft\.WindowsDesktop\.App 8\." } | Select-Object -First 1)
            Write-Ok ".NET 8 Windows Desktop Runtime found: $($version.Trim())"
            Write-Log ".NET 8 found: $version"
            return $true
        }
    }
    catch { }

    # Check registry as fallback
    $regPath = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (Test-Path $regPath) {
        $installed = Get-ItemProperty $regPath -ErrorAction SilentlyContinue
        $versions = $installed.PSObject.Properties | Where-Object { $_.Name -like "8.*" }
        if ($versions) {
            Write-Ok ".NET 8 Windows Desktop Runtime found (registry)"
            return $true
        }
    }

    Write-Warn ".NET 8 Windows Desktop Runtime not found"
    return $false
}

function Install-DotNet8 {
    if (!(Prompt-Continue ".NET 8 Windows Desktop Runtime is required. Download and install it now?")) {
        Write-Fail "Aborted. Please install .NET 8 manually from https://dot.net/8"
        return $false
    }

    $installer = Join-Path $TempDir "dotnet8-desktop-runtime.exe"

    if (!(Download-File -Url $DotNetDesktop -Destination $installer -Label ".NET 8 Desktop Runtime")) {
        return $false
    }

    Write-Info "Installing .NET 8 Desktop Runtime (this may take a minute)..."
    Write-Log "Running .NET installer: $installer"

    $proc = Start-Process -FilePath $installer `
        -ArgumentList "/install", "/quiet", "/norestart", "/log", "$TempDir\dotnet_install.log" `
        -Wait -PassThru

    if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
        Write-Ok ".NET 8 Desktop Runtime installed successfully"
        Write-Log ".NET install exit code: $($proc.ExitCode)"
        return $true
    }
    else {
        Write-Fail ".NET installation failed (exit code $($proc.ExitCode))"
        Write-Fail "Please install manually: https://dot.net/8"
        Write-Log ".NET install failed, exit: $($proc.ExitCode)"
        return $false
    }
}

function Test-VCRedist {
    Write-Log "Checking VC++ Redistributable..."

    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    )

    foreach ($key in $keys) {
        if (Test-Path $key) {
            $installed = (Get-ItemProperty $key -ErrorAction SilentlyContinue).Installed
            if ($installed -eq 1) {
                Write-Ok "Visual C++ Redistributable found"
                Write-Log "VC++ Redist found at $key"
                return $true
            }
        }
    }

    Write-Warn "Visual C++ Redistributable not found (needed for SharpDX)"
    return $false
}

function Install-VCRedist {
    if (!(Prompt-Continue "Visual C++ Redistributable is required. Download and install it now?")) {
        Write-Warn "Skipping VC++ Redist — app may not start correctly"
        return $true  # Not fatal, let them try
    }

    $url       = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    $installer = Join-Path $TempDir "vc_redist.x64.exe"

    if (!(Download-File -Url $url -Destination $installer -Label "VC++ Redistributable 2022")) {
        return $false
    }

    Write-Info "Installing VC++ Redistributable..."
    $proc = Start-Process -FilePath $installer -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru

    if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
        Write-Ok "VC++ Redistributable installed successfully"
        return $true
    }
    else {
        Write-Warn "VC++ install returned exit code $($proc.ExitCode) — continuing anyway"
        return $true
    }
}

function Test-SDL2 {
    Write-Log "Checking SDL2.dll..."
    
    $sdlPath = Join-Path (Split-Path $AppExe -Parent) "SDL2.dll"
    
    if (Test-Path $sdlPath) {
        Write-Ok "SDL2.dll found (for PlayStation controller support)"
        Write-Log "SDL2 found at: $sdlPath"
        return $true
    }

    Write-Warn "SDL2.dll not found — PlayStation controllers won't work"
    Write-Log "SDL2.dll not found at: $sdlPath"
    return $false
}

function Install-SDL2 {
    if (!(Prompt-Continue "SDL2.dll enables PlayStation controller support. Download it now?")) {
        Write-Warn "Skipping SDL2 — only Xbox controllers will work"
        return $true
    }

    $sdlVersion = "2.30.0"
    $url = "https://github.com/libsdl-org/SDL/releases/download/release-$sdlVersion/SDL2-$sdlVersion-win32-x64.zip"
    $zipPath = Join-Path $TempDir "SDL2.zip"
    $extractPath = Join-Path $TempDir "SDL2"

    if (!(Download-File -Url $url -Destination $zipPath -Label "SDL2 $sdlVersion")) {
        Write-Warn "SDL2 download failed — continuing without PlayStation support"
        return $true
    }

    Write-Info "Extracting SDL2.dll..."
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    $dllSource = Join-Path $extractPath "SDL2.dll"
    $dllDest   = Join-Path (Split-Path $AppExe -Parent) "SDL2.dll"

    if (Test-Path $dllSource) {
        Copy-Item $dllSource $dllDest -Force
        Write-Ok "SDL2.dll installed — PlayStation controllers now supported"
        Write-Log "SDL2.dll copied to: $dllDest"
        return $true
    } else {
        Write-Warn "SDL2.dll not found in archive — continuing anyway"
        return $true
    }
}

function Test-XboxController {
    Write-Log "Checking XInput / Xbox controller driver..."

    $xinput = @(
        "$env:SystemRoot\System32\xinput1_4.dll",
        "$env:SystemRoot\System32\xinput9_1_0.dll"
    )

    foreach ($dll in $xinput) {
        if (Test-Path $dll) {
            Write-Ok "XInput driver found: $(Split-Path $dll -Leaf)"
            Write-Log "XInput found: $dll"
            return $true
        }
    }

    Write-Warn "XInput DLL not found — connect your controller and install drivers if needed"
    Write-Log "XInput DLL not found"
    return $true  # Not fatal
}

function Test-AppExists {
    if (Test-Path $AppExe) {
        Write-Ok "RagnaController.exe found"
        Write-Log "App found at: $AppExe"
        return $true
    }

    Write-Fail "RagnaController.exe not found at: $AppExe"
    Write-Log  "App not found: $AppExe"
    return $false
}

# ── Main ───────────────────────────────────────────────────────────────────────

function Main {
    New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
    Write-Log "=== RagnaController Setup started ==="
    Write-Log "PowerShell: $($PSVersionTable.PSVersion)"

    Write-Header

    Write-Host "  Checking system requirements..." -ForegroundColor DarkGray
    Write-Host "  ─────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ""

    $allGood = $true

    # 1. Windows
    if (!(Test-WindowsVersion)) { $allGood = $false }
    Write-Host ""

    # 2. .NET 8
    if (!(Test-DotNet8)) {
        if (!(Install-DotNet8)) { $allGood = $false }
    }
    Write-Host ""

    # 3. VC++ Redist
    if (!(Test-VCRedist)) {
        Install-VCRedist | Out-Null
    }
    Write-Host ""

    # 4. XInput
    Test-XboxController | Out-Null
    Write-Host ""

    # 5. SDL2 (PlayStation controllers)
    if (!(Test-SDL2)) {
        Install-SDL2 | Out-Null
    }
    Write-Host ""

    # 6. App binary
    if (!(Test-AppExists)) { $allGood = $false }

    Write-Host ""
    Write-Host "  ─────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ""

    if (!$allGood) {
        Write-Fail "Setup could not complete. Please fix the issues above and try again."
        Write-Host ""
        Write-Host "  Log file: $LogFile" -ForegroundColor DarkGray
        Write-Host ""
        Read-Host "  Press Enter to exit"
        exit 1
    }

    Write-Ok "All requirements satisfied!"
    Write-Log "All checks passed. Launching app."
    Write-Host ""
    Write-Info "Launching RagnaController..."
    Write-Host ""

    Start-Sleep -Milliseconds 800
    Start-Process -FilePath $AppExe
    Write-Log "App launched."
}

Main
