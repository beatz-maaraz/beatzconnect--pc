; ============================================================
;  Beatz Control — Inno Setup Installer Script
;  Target:  Beatz Control v2.0
;  Relay:   https://backend-n4yg.onrender.com
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

; ── Install paths ────────────────────────────────────────────
DefaultDirName           = {autopf}\{#AppName}
DefaultGroupName         = {#AppName}
DisableProgramGroupPage  = yes

; ── Output ───────────────────────────────────────────────────
OutputDir                = ..\dist
OutputBaseFilename       = BeatzControl_Setup_v{#AppVersion}
SetupIconFile            = assets\icon.ico

; ── Compression ──────────────────────────────────────────────
Compression              = lzma2/ultra64
SolidCompression         = yes
LZMAUseSeparateProcess   = yes

; ── Appearance ───────────────────────────────────────────────
WizardStyle              = modern
WizardSizePercent        = 120
DisableWelcomePage       = no
ShowLanguageDialog       = no

; ── Privileges ───────────────────────────────────────────────
PrivilegesRequired       = admin
PrivilegesRequiredOverridesAllowed = dialog

; ── Architecture ─────────────────────────────────────────────
ArchitecturesAllowed            = x64compatible
ArchitecturesInstallIn64BitMode = x64compatible

; ── Misc ─────────────────────────────────────────────────────
UninstallDisplayIcon     = {app}\{#AppExeName}
UninstallDisplayName     = {#AppName}
VersionInfoVersion       = {#AppVersion}
VersionInfoCompany       = {#AppPublisher}
VersionInfoDescription   = Beatz Control — Remote PC Controller
VersionInfoProductName   = {#AppName}
CloseApplications        = yes
RestartApplications      = no

; ── Signing (optional — uncomment and fill in for production) ─
; SignTool = signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /f "cert.pfx" /p "password" $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
; Custom wizard page text
WelcomeLabel1=Welcome to the {#AppName} Setup Wizard
WelcomeLabel2=This will install {#AppName} v{#AppVersion} on your computer.%n%nBeatz Control connects to the cloud relay at backend-n4yg.onrender.com so your phone can control your PC from anywhere.%n%nClick Next to continue or Cancel to exit.
FinishedLabel=Setup has finished installing {#AppName} on your computer.%n%nYou can launch the application from the Start Menu or the Desktop shortcut.

[Tasks]
; Tick boxes shown on the "Select Additional Tasks" page
Name: "desktopicon";       Description: "Create a &Desktop shortcut";              GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupentry";      Description: "Start {#AppName} automatically with Windows"; GroupDescription: "Startup:";            Flags: unchecked
Name: "firewallrule";      Description: "Add Windows Firewall exception (port 8080 — LAN fallback)"; GroupDescription: "Network:"; Flags: unchecked

[Files]
; ── Main application ──────────────────────────────────────────
Source: "..\PCControl.UI\bin\Release\net8.0-windows\*";  DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Optionally bundle .NET 8 runtime (if not using prerequisites) ──
; Source: "redist\windowsdesktop-runtime-8.0.x-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
; Start Menu shortcut
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional task)
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch the app after install
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent

[Registry]
; Windows startup entry (optional task)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Code]
// ──────────────────────────────────────────────────────────────
//  Custom Pascal Script
// ──────────────────────────────────────────────────────────────

// Check for .NET 8 Desktop Runtime before install
function IsDotNet8Installed(): Boolean;
var
  Key: String;
begin
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  Result := RegKeyExists(HKLM, Key) or RegKeyExists(HKCU, Key);
  // Fallback: check the known install path
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
    Msg := '.NET 8 Desktop Runtime was not detected on this machine.' + #13#10 +
           'Please download and install it from:' + #13#10 +
           'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10#10 +
           'Click OK to open the download page.';
    if MsgBox(Msg, mbInformation, MB_OKCANCEL) = IDOK then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, Msg);
    Result := False;
  end;
end;

// Add firewall rule via netsh if the task was selected
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

// Remove firewall rule on uninstall
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
