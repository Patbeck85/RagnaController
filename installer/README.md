# RagnaController — Installer & Build System

## Files

| File | Zweck |
|---|---|
| `installer/Setup.bat` | Doppelklick-Installer für Endnutzer (startet Setup.ps1 als Admin) |
| `installer/Setup.ps1` | PowerShell-Installer: prüft .NET 8 / VC++ / XInput, lädt fehlende Komponenten herunter |
| `installer/RagnaController.iss` | Inno Setup Script → baut `RagnaController-Setup-v1.0.0.exe` |
| `scripts/build.ps1` | Master-Build-Script: kompiliert, published, packt ZIP + optional Installer |
| `RagnaController.bat` | Quick-Launcher für portable Version |

---

## Build-Anleitung

### Voraussetzungen

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) *(nur für .exe Installer)*

### Befehle

```powershell
# 1. Nur App bauen + portable ZIP erstellen
.\scripts\build.ps1

# 2. App + .exe Installer (Inno Setup muss installiert sein)
.\scripts\build.ps1 -BuildInstaller

# 3. Self-contained (kein .NET nötig auf Zielrechner, ~150 MB)
.\scripts\build.ps1 -SelfContained

# 4. Alles neu von Grund auf
.\scripts\build.ps1 -Clean -BuildInstaller
```

### Ausgabe

```
publish\                              ← App-Binaries
RagnaController-v1.0.0-portable.zip  ← Portable ZIP
installer\Output\
  RagnaController-Setup-v1.0.0.exe   ← Windows Installer
```

---

## Was der Installer prüft

```
Setup.bat (als Admin)
  └── Setup.ps1
        ├── ✔ Windows 10 / 11 Version
        ├── ✔ .NET 8 Windows Desktop Runtime
        │     └── falls fehlt: Download + Silent Install von aka.ms/dotnet/8.0/...
        ├── ✔ Visual C++ Redistributable 2022 x64
        │     └── falls fehlt: Download + Silent Install von aka.ms/vs/17/release/...
        ├── ✔ XInput Treiber (Hinweis wenn fehlt)
        └── ✔ RagnaController.exe vorhanden
              └── falls alles OK: App starten
```

---

## GitHub Release Checklist

Für jeden Release folgende Dateien hochladen:

- [ ] `RagnaController-Setup-v{version}.exe` — Windows Installer
- [ ] `RagnaController-v{version}-portable.zip` — Portable ZIP
- [ ] Source Code (automatisch von GitHub)
