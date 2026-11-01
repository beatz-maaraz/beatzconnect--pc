; ============================================================
;  Beatz Control — Inno Setup Step-by-Step Installation Wizard
;  Target:      Beatz Control v2.0
;  Publisher:   Beatz Control Technologies
;  Relay:       https://backend-n4yg.onrender.com
; ============================================================

#define AppName      "Beatz Control"
#define AppVersion   "2.0.0"
#define AppPublisher "Beatz"
#define AppExeName   "PCControl.UI.exe"
#define AppURL       "https://backend-n4yg.onrender.com"

[Setup]
; ── Identity & versioning ────────────────────────────────────
AppId                    = {{E4A7C2B1-9F3D-4E8A-B6C5-2D1F0A3E7B9C}}
AppName                  = {#AppName}
AppVersion               = {#AppVersion}
AppPublisherURL          = {#AppURL}
AppSupportURL            = {#AppURL}
AppUpdatesURL            = {#AppURL}
AppPublisher             = {#AppPublisher}

; ── Step-by-Step Installation Wizard Flow ────────────────────
; 1. Welcome Page (Active)
DisableWelcomePage       = no

; 2. License Agreement Screen
LicenseFile              = LICENSE.txt

; 3. Destination Directory Selection
DisableDirPage           = no
DefaultDirName           = {autopf}\{#AppName}
AllowNoIcons             = no

; 4. Program Group Selection
DisableProgramGroupPage  = no
DefaultGroupName         = {#AppName}

; 5. Installation Progress Bar & Status (Active by default during install)
ShowComponentSizes       = yes

; ── Output & Compression ─────────────────────────────────────
OutputDir                = ..\dist
OutputBaseFilename       = BeatzControl_Setup_v{#AppVersion}
Compression              = lzma2/ultra64
SolidCompression         = yes
LZMAUseSeparateProcess   = yes

; ── Modern Fluent Wizard Styling ─────────────────────────────
WizardStyle              = modern
WizardSizePercent        = 120
WizardResizable          = no
ShowLanguageDialog       = no

; ── Permissions & System Requirements ────────────────────────
PrivilegesRequired       = admin
PrivilegesRequiredOverridesAllowed = dialog
ArchitecturesAllowed            = x64compatible
ArchitecturesInstallIn64BitMode = x64compatible

; ── App State & Shutdown Handling ────────────────────────────
CloseApplications        = yes
RestartApplications      = no
UninstallDisplayIcon     = {app}\{#AppExeName}
UninstallDisplayName     = {#AppName}
VersionInfoVersion       = {#AppVersion}
VersionInfoCompany       = {#AppPublisher}
VersionInfoDescription   = Beatz Control — Professional Remote PC Automation Suite
VersionInfoProductName   = {#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to the {#AppName} Setup Wizard
WelcomeLabel2=This wizard will guide you through installing {#AppName} v{#AppVersion} on your workstation.%n%nBeatz Control pairs your PC with remote mobile clients locally over high-throughput Wi-Fi or globally via secure relay.%n%nPlease review the license agreement and choose your installation options on the next screens.
LicenseLabel1=End User License Agreement
LicenseLabel2=Please review the following license terms before proceeding with the installation.
FinishedHeadingLabel=Completing the {#AppName} Setup Wizard
FinishedLabel=Setup has successfully installed {#AppName} on your workstation.%n%nThe application can now be launched directly or accessed anytime via your Start Menu and Desktop shortcuts.

[Tasks]
Name: "desktopicon";   Description: "Create a &Desktop shortcut";                      GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupentry";  Description: "Launch {#AppName} automatically on Windows logon"; GroupDescription: "System Startup:";      Flags: unchecked
Name: "firewallrule";  Description: "Configure Windows Firewall rule for local TCP (Port 8080)"; GroupDescription: "Network Integration:"; Flags: unchecked

[Files]
; ── Application binaries & dependencies ──────────────────────
Source: "..\PCControl.UI\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"" --autostart --minimized"; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; Post-installation launch prompt with progress completion
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent

[Code]
// ──────────────────────────────────────────────────────────────
//  Custom Pascal Script Checks & Network Setup
// ──────────────────────────────────────────────────────────────

function IsDotNet8Installed(): Boolean;
var
  Key: String;
begin
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  Result := RegKeyExists(HKLM, Key) or RegKeyExists(HKCU, Key);
  if not Result then
    Result := DirExists(ExpandConstant('{pf64}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;

function InitializeSetup(): Boolean;
var
  Msg: String;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    Msg := '.NET 8 Desktop Runtime was not detected on this system.' + #13#10 +
           'Beatz Control requires .NET 8 to run its modern Fluent interface.' + #13#10#10 +
           'Click OK to visit the Microsoft download page, or Cancel to exit.';
    if MsgBox(Msg, mbInformation, MB_OKCANCEL) = IDOK then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, Msg);
    Result := False;
  end;
end;

// Configure Windows Firewall rule on install if enabled
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if IsTaskSelected('firewallrule') then
    begin
      Exec(
        ExpandConstant('{sys}\netsh.exe'),
        'advfirewall firewall add rule name="Beatz Control LAN" dir=in action=allow protocol=TCP localport=8080',
        '',
        SW_HIDE,
        ewWaitUntilTerminated,
        ResultCode
      );
    end;
  end;
end;

// Clean up firewall rules during uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Exec(
      ExpandConstant('{sys}\netsh.exe'),
      'advfirewall firewall delete rule name="Beatz Control LAN"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode
    );
  end;
end;
