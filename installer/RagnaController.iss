; ─────────────────────────────────────────────────────────────────────────────
; RagnaController — Inno Setup Script
;
; Build with: Inno Setup Compiler (https://jrsoftware.org/isinfo.php)
; Output:     installer\Output\RagnaController-Setup-v1.0.0.exe
;
; This script:
;  1. Bundles the compiled app + all dependencies
;  2. Checks for .NET 8 Windows Desktop Runtime
;  3. Downloads and installs .NET 8 if missing
;  4. Creates Start Menu + Desktop shortcut
;  5. Registers in Windows Add/Remove Programs
; ─────────────────────────────────────────────────────────────────────────────

#define MyAppName      "RagnaController"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "RagnaController Contributors"
#define MyAppURL       "https://github.com/yourusername/RagnaController"
#define MyAppExeName   "RagnaController.exe"
#define MyAppID        "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId                     = {#MyAppID}
AppName                   = {#MyAppName}
AppVersion                = {#MyAppVersion}
AppPublisher              = {#MyAppPublisher}
AppPublisherURL           = {#MyAppURL}
AppSupportURL             = {#MyAppURL}/issues
AppUpdatesURL             = {#MyAppURL}/releases

DefaultDirName            = {autopf}\{#MyAppName}
DefaultGroupName          = {#MyAppName}
AllowNoIcons              = yes

OutputDir                 = installer\Output
OutputBaseFilename        = RagnaController-Setup-v{#MyAppVersion}

; Compression
Compression               = lzma2/ultra64
SolidCompression          = yes
LZMAUseSeparateProcess    = yes

; UI
WizardStyle               = modern
WizardSizePercent         = 120
SetupIconFile             = src\RagnaController\Assets\icon.ico

; Uninstaller
UninstallDisplayIcon      = {app}\{#MyAppExeName}
UninstallDisplayName      = {#MyAppName} {#MyAppVersion}

; Privileges
PrivilegesRequired        = admin
PrivilegesRequiredOverridesAllowed = dialog

; Min Windows version: Windows 10
MinVersion                = 10.0.17763

; Architecture
ArchitecturesInstallIn64BitMode = x64
ArchitecturesAllowed      = x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.CheckingDotNet   = Checking .NET 8 Windows Desktop Runtime...
english.InstallingDotNet = Installing .NET 8 Windows Desktop Runtime...
english.DotNetRequired   = .NET 8 Windows Desktop Runtime is required but could not be installed.%n%nPlease download it manually from:%nhttps://dot.net/8
english.DotNetOK         = .NET 8 Runtime: OK
german.CheckingDotNet    = Prüfe .NET 8 Windows Desktop Runtime...
german.InstallingDotNet  = Installiere .NET 8 Windows Desktop Runtime...
german.DotNetRequired    = .NET 8 Windows Desktop Runtime wird benötigt, konnte aber nicht installiert werden.%n%nBitte manuell herunterladen:%nhttps://dot.net/8
german.DotNetOK          = .NET 8 Runtime: OK

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; App binaries (publish output — run: dotnet publish -c Release -r win-x64 --self-contained false first)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Installer helpers
Source: "installer\Setup.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "installer\Setup.bat"; DestDir: "{app}\installer"; Flags: ignoreversion

; Docs
Source: "README.md";            DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE";              DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "docs\architecture.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";                Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";      Filename: "{uninstallexe}"
Name: "{group}\GitHub Repository";           Filename: "{#MyAppURL}"
Name: "{commondesktop}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch after install (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up user data on uninstall (only app dir, NOT %AppData% profiles)
Type: filesandordirs; Name: "{app}"

; ─────────────────────────────────────────────────────────────────────────────
; .NET 8 Detection & Download (Pascal Script)
; ─────────────────────────────────────────────────────────────────────────────

[Code]

const
  DotNetDesktopRuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  DotNetMinVersion        = '8.0.0';

var
  DotNetInstallerPath: String;

// ── Detect .NET 8 Windows Desktop Runtime ─────────────────────────────────────

function DotNet8IsInstalled(): Boolean;
var
  Key:     String;
  Installed: Cardinal;
  Versions:  TArrayOfString;
  I:         Integer;
begin
  Result := False;

  // Try registry: HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';

  if RegGetValueNames(HKLM, Key, Versions) then
  begin
    for I := 0 to GetArrayLength(Versions) - 1 do
    begin
      if Pos('8.', Versions[I]) = 1 then
      begin
        Result := True;
        Log('.NET 8 Desktop Runtime found in registry: ' + Versions[I]);
        Exit;
      end;
    end;
  end;

  // Fallback: Check WOW6432Node
  Key := 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetValueNames(HKLM, Key, Versions) then
  begin
    for I := 0 to GetArrayLength(Versions) - 1 do
    begin
      if Pos('8.', Versions[I]) = 1 then
      begin
        Result := True;
        Log('.NET 8 Desktop Runtime found in WOW6432Node: ' + Versions[I]);
        Exit;
      end;
    end;
  end;

  Log('.NET 8 Desktop Runtime NOT found.');
end;

// ── Download Callback ─────────────────────────────────────────────────────────

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax <> 0 then
    WizardForm.StatusLabel.Caption := FmtMessage(CustomMessage('InstallingDotNet') + ' %1%%', [IntToStr((Progress * 100) div ProgressMax)])
  else
    WizardForm.StatusLabel.Caption := CustomMessage('InstallingDotNet');
  Result := True;
end;

// ── InitializeSetup — Run before wizard appears ────────────────────────────────

function InitializeSetup(): Boolean;
begin
  Result := True;
  DotNetInstallerPath := '';
end;

// ── PrepareToInstall — Download .NET if needed ────────────────────────────────

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  TempDir:    String;
begin
  Result := '';

  WizardForm.StatusLabel.Caption := CustomMessage('CheckingDotNet');

  if DotNet8IsInstalled() then
  begin
    Log(CustomMessage('DotNetOK'));
    Exit;
  end;

  // .NET not found — download
  TempDir := ExpandConstant('{tmp}');
  DotNetInstallerPath := TempDir + '\dotnet8-desktop-runtime.exe';

  WizardForm.StatusLabel.Caption := CustomMessage('InstallingDotNet');
  Log('Downloading .NET 8 Desktop Runtime from: ' + DotNetDesktopRuntimeUrl);

  if not DownloadTemporaryFile(DotNetDesktopRuntimeUrl, 'dotnet8-desktop-runtime.exe', '', @OnDownloadProgress) then
  begin
    Result := CustomMessage('DotNetRequired');
    Exit;
  end;

  // Run installer silently
  Log('Running .NET installer: ' + DotNetInstallerPath);
  if not Exec(DotNetInstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Failed to run .NET installer. Code: ' + IntToStr(ResultCode));
    Result := CustomMessage('DotNetRequired');
    Exit;
  end;

  // 0 = success, 3010 = reboot required
  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    Log('.NET installer failed with code: ' + IntToStr(ResultCode));
    Result := CustomMessage('DotNetRequired');
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True;

  Log('.NET 8 Desktop Runtime installed successfully.');
end;

// ── CurStepChanged — Post-install actions ─────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    Log('Installation complete.');
  end;
end;
