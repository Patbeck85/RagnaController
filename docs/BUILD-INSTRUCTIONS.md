# RagnaController — Build Instructions

## Prerequisites

| Tool | Version | Download |
|---|---|---|
| .NET 8 SDK | 8.0+ | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Windows 10/11 | Required | (WPF is Windows-only) |

Verify your SDK by running `dotnet --version` in your terminal. It must return `8.x.x` or higher.

---

## 🚀 The Automated Build Tool (START.bat)

The absolute best and fastest way to build RagnaController is using the included `START.bat` in the root directory.

1. Double-click `START.bat`.
2. The script will automatically check if you have the `.NET 8 SDK` installed. **If not, it will securely download and install it for you!**
3. Choose your desired output format from the terminal menu:
   - `[1] Windows Framework-dependent` (Tiny ~2MB file, requires user to have .NET 8)
   - `[2] Windows Self-Contained` (Large ~140MB file, runs everywhere instantly)
   - `[3] Steam Deck` (Optimized for Linux/Proton)
   - `[4] Build All`
4. The script automatically reads your version number from the `.csproj` file, compiles the code, copies the documentation, and creates a clean `RagnaController_v1.x.x.zip` file inside the `publish/` directory.

---

## Manual Step-by-Step (Command Line)

If you prefer to build manually without the batch script:

```bash
cd path\to\RagnaController

# Restore packages
dotnet restore src/RagnaController/RagnaController.csproj

# Build (Debug)
dotnet build src/RagnaController/RagnaController.csproj -c Debug

# Publish release (Framework-dependent)
dotnet publish src/RagnaController/RagnaController.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/