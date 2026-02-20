<#
.SYNOPSIS
    RagnaController — Master Build & Package Script

.DESCRIPTION
    Compiles the app, runs dotnet publish, and optionally builds the Inno Setup installer.
    
    Usage:
        .\scripts\build.ps1                   # Build Release
        .\scripts\build.ps1 -Configuration Debug
        .\scripts\build.ps1 -BuildInstaller   # Also build .exe installer (requires Inno Setup)
        .\scripts\build.ps1 -SelfContained    # Bundle .NET runtime (larger, no install needed)
        .\scripts\build.ps1 -Clean            # Clean before build

.EXAMPLE
    .\scripts\build.ps1 -BuildInstaller
#>

[CmdletBinding()]
param(
    [string] $Configuration  = "Release",
    [string] $Runtime        = "win-x64",
    [string] $Version        = "1.0.0",
    [switch] $SelfContained,
    [switch] $BuildInstaller,
    [switch] $Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Paths ──────────────────────────────────────────────────────────────────────

$Root        = Split-Path $PSScriptRoot -Parent
$SrcProject  = Join-Path $Root "src\RagnaController\RagnaController.csproj"
$PublishDir  = Join-Path $Root "publish"
$InstallerDir= Join-Path $Root "installer"
$IssFile     = Join-Path $InstallerDir "RagnaController.iss"
$OutDir      = Join-Path $InstallerDir "Output"
$ZipOut      = Join-Path $Root "RagnaController-v$Version-portable.zip"

# ── Colors ─────────────────────────────────────────────────────────────────────

function h1 { param([string]$t)  Write-Host "`n  ═══════════════════════════════════════" -ForegroundColor DarkCyan
                                  Write-Host "   $t" -ForegroundColor Cyan
                                  Write-Host "  ═══════════════════════════════════════" -ForegroundColor DarkCyan }
function ok { param([string]$t)  Write-Host "  ✔  $t" -ForegroundColor Green }
function inf{ param([string]$t)  Write-Host "  →  $t" -ForegroundColor Gray }
function err{ param([string]$t)  Write-Host "  ✖  $t" -ForegroundColor Red }

# ── Prerequisites check ────────────────────────────────────────────────────────

h1 "Checking Prerequisites"

# .NET SDK
try {
    $sdkVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw }
    ok ".NET SDK: $sdkVersion"
}
catch {
    err ".NET SDK not found. Install from https://dot.net/8"
    exit 1
}

# SDK is >= 8
$major = [int]($sdkVersion.Split(".")[0])
if ($major -lt 8) {
    err ".NET SDK $sdkVersion found, but 8.0+ required."
    exit 1
}

# Inno Setup (optional)
$innoPath = ""
$innoLocations = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
foreach ($loc in $innoLocations) {
    if (Test-Path $loc) { $innoPath = $loc; break }
}

if ($BuildInstaller) {
    if ($innoPath) {
        ok "Inno Setup found: $innoPath"
    }
    else {
        err "Inno Setup 6 not found. Download from https://jrsoftware.org/isinfo.php"
        err "Skipping installer build. Run without -BuildInstaller to build portable ZIP only."
        $BuildInstaller = $false
    }
}

# ── Clean ──────────────────────────────────────────────────────────────────────

if ($Clean) {
    h1 "Cleaning"
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force; ok "Cleaned: publish\" }
    if (Test-Path $OutDir)     { Remove-Item $OutDir     -Recurse -Force; ok "Cleaned: installer\Output\" }
    if (Test-Path $ZipOut)     { Remove-Item $ZipOut     -Force;          ok "Cleaned: portable ZIP" }
    & dotnet clean $SrcProject -c $Configuration --nologo -v q
}

# ── Restore ────────────────────────────────────────────────────────────────────

h1 "Restoring NuGet Packages"
& dotnet restore $SrcProject --nologo
if ($LASTEXITCODE -ne 0) { err "Restore failed"; exit 1 }
ok "Restore complete"

# ── Build ──────────────────────────────────────────────────────────────────────

h1 "Building ($Configuration)"

$buildArgs = @(
    "build", $SrcProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--nologo",
    "-v", "minimal",
    "/p:Version=$Version"
)

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { err "Build failed"; exit 1 }
ok "Build succeeded"

# ── Publish ────────────────────────────────────────────────────────────────────

h1 "Publishing"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

$publishArgs = @(
    "publish", $SrcProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $PublishDir,
    "--nologo",
    "/p:Version=$Version",
    "/p:PublishSingleFile=false"   # Set true for single .exe (slower start, but one file)
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
    inf "Self-contained publish — .NET runtime will be bundled (~150 MB)"
}
else {
    $publishArgs += "--no-self-contained"
    inf "Framework-dependent publish — .NET 8 Runtime required on target machine"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { err "Publish failed"; exit 1 }
ok "Published to: $PublishDir"

# ── Copy extra files to publish dir ────────────────────────────────────────────

Copy-Item (Join-Path $Root "README.md")    $PublishDir -Force
Copy-Item (Join-Path $Root "LICENSE")      (Join-Path $PublishDir "LICENSE.txt") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $PublishDir "docs") | Out-Null
Copy-Item (Join-Path $Root "docs\architecture.md") (Join-Path $PublishDir "docs\") -Force -ErrorAction SilentlyContinue

ok "Extra files copied"

# ── Create portable ZIP ────────────────────────────────────────────────────────

h1 "Creating Portable ZIP"

if (Test-Path $ZipOut) { Remove-Item $ZipOut -Force }

# Include installer scripts in ZIP
$zipStaging = Join-Path $env:TEMP "RagnaBuild_$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force -Path $zipStaging | Out-Null
Copy-Item "$PublishDir\*" $zipStaging -Recurse -Force
New-Item -ItemType Directory -Force -Path "$zipStaging\installer" | Out-Null
Copy-Item (Join-Path $InstallerDir "Setup.bat") "$zipStaging\installer\" -Force
Copy-Item (Join-Path $InstallerDir "Setup.ps1") "$zipStaging\installer\" -Force

Compress-Archive -Path "$zipStaging\*" -DestinationPath $ZipOut -CompressionLevel Optimal
Remove-Item $zipStaging -Recurse -Force

$size = "{0:N1} MB" -f ((Get-Item $ZipOut).Length / 1MB)
ok "Portable ZIP: $([System.IO.Path]::GetFileName($ZipOut)) ($size)"

# ── Inno Setup Installer ───────────────────────────────────────────────────────

if ($BuildInstaller) {
    h1 "Building Installer (.exe)"
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    $issArgs = @(
        $IssFile,
        "/DMyAppVersion=$Version",
        "/Q"
    )

    & $innoPath @issArgs
    if ($LASTEXITCODE -ne 0) { err "Inno Setup build failed"; exit 1 }

    $setupExe = Get-ChildItem $OutDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($setupExe) {
        $size2 = "{0:N1} MB" -f ($setupExe.Length / 1MB)
        ok "Installer: $($setupExe.Name) ($size2)"
    }
}

# ── Summary ────────────────────────────────────────────────────────────────────

h1 "Build Complete"

Write-Host ""
Write-Host "  Outputs:" -ForegroundColor DarkGray
inf "  App binaries:   publish\"
inf "  Portable ZIP:   $([System.IO.Path]::GetFileName($ZipOut))"
if ($BuildInstaller -and $setupExe) {
    inf "  Installer:      installer\Output\$($setupExe.Name)"
}
Write-Host ""
Write-Host "  ✔  Done!" -ForegroundColor Green
Write-Host ""
