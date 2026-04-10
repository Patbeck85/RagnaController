# RagnaController — Build Instructions

## Prerequisites

| Tool | Version | Download |
|---|---|---|
| .NET 8 SDK | 8.0+ | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Inno Setup 6 | Optional (installer only) | https://jrsoftware.org/isdl.php |
| Windows 10/11 | Required | (WPF is Windows-only) |

Verify your SDK:
```
dotnet --version
```
Must return `8.x.x` or higher.

---

## Quick Build

All commands run from the solution root (where `RagnaController.sln` lives).

### Framework-dependent (small EXE, requires .NET runtime installed)
```
dotnet publish src/RagnaController/RagnaController.csproj ^
  -c Release -r win-x64 --no-self-contained ^
  -p:PublishSingleFile=true -o publish/
```

### Self-contained (no runtime needed, larger EXE ~140 MB)
```
dotnet publish src/RagnaController/RagnaController.csproj ^
  -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o publish/
```

### Debug build (with console window for logs)
```
dotnet build src/RagnaController/RagnaController.csproj -c Debug
```

### Clean
```
dotnet clean src/RagnaController/RagnaController.csproj
```

---

## What Gets Built

After publish, the `publish/` folder contains:

```
publish/
├── RagnaController.exe        ← single-file executable
├── GetDotNet8.bat             ← copied automatically by post-build target
└── Assets/
    └── icon.ico               ← needed at runtime for tray icon
```

The `.csproj` post-build target also creates a ZIP automatically:
```
RagnaController_v1.0.0.zip    ← placed next to the .sln file
```

---

## NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| SharpDX.XInput | 4.2.0 | XInput controller polling |
| System.Management | 8.0.0 | WMI queries (controller brand detection) |
| System.Text.Json | 9.0.0 | Profile / macro / settings serialization |

These are restored automatically by `dotnet restore` or on first build.

---

## Manual Step-by-Step

```
cd path\to\RagnaController

# Restore packages
dotnet restore src/RagnaController/RagnaController.csproj

# Build (Debug)
dotnet build src/RagnaController/RagnaController.csproj -c Debug

# Run directly
dotnet run --project src/RagnaController/RagnaController.csproj

# Publish release
dotnet publish src/RagnaController/RagnaController.csproj ^
  -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -o publish/
```

---

## Changing the Version Number

Edit `src/RagnaController/RagnaController.csproj`:

```xml
<Version>1.0.0</Version>
```

The ZIP filename in the post-build target uses `$(Version)` automatically.  
Also update the splash screen version label in `SplashWindow.xaml`:

```xml
<TextBlock Text="v1.0.0" .../>
```

---

## Creating an Installer (Inno Setup)

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. Build the release publish first (see above)
3. Compile `installer/RagnaController.iss` in Inno Setup IDE or via CLI:
   ```
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\RagnaController.iss
   ```
4. Installer EXE created at `installer/Output/RagnaController_Setup_v1.0.0.exe`

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `error MSB4018: The "ResolveAssemblyReference" task failed` | Run `dotnet restore` first |
| `SharpDX.XInput not found` | Run `dotnet restore` — NuGet download required |
| `NETSDK1045: current SDK does not support .NET 8` | Install .NET 8 SDK from microsoft.com |
| EXE crashes on launch with runtime dialog | Install .NET 8 Windows Desktop Runtime (not just base) |
| Tray icon shows generic Windows icon | Ensure `Assets/icon.ico` is in the same folder as the EXE |
| WPF designer not showing | Requires Visual Studio 2022 with .NET desktop workload |

---

## Build Times (approximate)

| Step | Time |
|---|---|
| `dotnet restore` (first time) | ~10–20 s |
| `dotnet build` (incremental) | ~3–5 s |
| `dotnet publish` (release) | ~8–15 s |

---

## Output File Sizes

| Build type | EXE size |
|---|---|
| Framework-dependent | ~500 KB |
| Self-contained | ~130 MB |
| Installer (Inno Setup) | ~80–140 MB |

---

## Distribution

**Recommended for releases:**
- Build framework-dependent + include `GetDotNet8.bat`
- ZIP the publish folder
- Users who already have .NET 8 get a tiny download (~500 KB)
- Users without it run `GetDotNet8.bat` for automatic install

**For standalone / offline deployment:**
- Use self-contained build
- No prerequisites needed on target machine
