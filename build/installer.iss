; Inno Setup script for DockerDesk (DD14).
;
; Build, from the repository root:
;   build\build-installer.cmd
; or by hand:
;   dotnet publish src\DockerDesk.Tray -c Release
;   "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" build\installer.iss
; Output: dist\DockerDesk-Setup.exe
;
; Every relative path below resolves against THIS file's directory (build\), so the ones pointing
; at the repository root are "..\"-relative.

#define MyAppName "DockerDesk"
#define MyAppPublisher "DockerDesk contributors"
#define MyAppUrl "https://github.com/alegauss/dockerdesk"
#define MyAppExeName "DockerDesk.exe"
#define MyPublishDir "..\src\DockerDesk.Tray\bin\Release\net10.0-windows\win-x64\publish"

; Read straight off the published .exe, which got it from <Version> in Directory.Build.props. There
; is no second version to bump here, and a PackagingTests case holds that string to "x.y.z" with no
; commit suffix — Add/Remove Programs shows this verbatim. Requires the publish to have run first.
#define MyAppVersion GetStringFileInfo(MyPublishDir + "\" + MyAppExeName, PRODUCT_VERSION)

[Setup]
AppId={{6B0E4D2A-9C77-4A31-8F5E-DOCKERDESK001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
VersionInfoVersion={#MyAppVersion}

; The whole point of the per-user install. `lowest` means no administrator prompt for the
; application: the audience is developers on managed corporate laptops, and a UAC dialog at install
; time is where a large share of them stop. The engine's WSL2 feature may still need elevation of its
; own, which is why the preflight below states that before anything is downloaded rather than a
; dialog appearing halfway through a provision.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; {app} is deliberately the same directory EnginePaths calls Root, so everything this tool owns —
; the executable, the downloads, the distribution, the docker CLI — is under one folder a person can
; find, and the uninstall has one place to ask about.
DefaultDirName={localappdata}\DockerDesk
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; WSL2 needs Windows 10 2004. Saying so here is cheaper than a preflight on a machine that was never
; going to work, and MinVersion is the one check Inno can make before anything is written.
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile=..\src\DockerDesk.Tray\DockerDesk.ico
OutputDir=..\dist
OutputBaseFilename=DockerDesk-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE

; DD21. Measured: launching the tray creates a NotifyIconSettings entry with IsPromoted absent, so
; the icon registers and Windows 11 files it into the overflow — the documented default for an icon
; the shell has not seen before. This tool does not promote itself out of it, so the install has to
; say where the icon went rather than leave somebody hunting for a state indicator that was promised
; as a glance. Shown as its own page, and skipped automatically in a silent install.
InfoAfterFile=after-install.txt

; The tray may be running from a previous install. Restart Manager closes it without forcing a
; reboot; RestartApplications=no because nothing here needs the machine restarted.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start {#MyAppName} with Windows"; GroupDescription: "Startup:"
; The engine is not started by installing. A resident background service is a stated non-goal, and
; an installer that leaves a container engine running is the weight this project is an answer to.
Name: "pathentry"; Description: "Put docker and dockerdesk on my PATH"; GroupDescription: "Command line:"

[Files]
; One file. That is DD14: one .exe to publish, to sign, to install and to hand somebody.
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; DD24. The agent surface is reached as `dockerdesk read ...`, which is the literal string an
; allowlist entry matches - `Bash(dockerdesk read:*)`. The .exe lives in {app} and only {app}\bin is
; on PATH, so without this the one command the whole read/do split exists to make grantable does not
; resolve at all. A forwarder rather than a second PATH entry: one name on PATH, one thing to remove.
Source: "dockerdesk.cmd"; DestDir: "{app}\bin"; Flags: ignoreversion; Tasks: pathentry

; DD32. How the surface is found, shipped beside it: a skill naming the verbs and the one rule, and
; the allowlist line that makes the read/do split pay. Laid down in {app}\agent and nowhere else -
; this install never touches a user's .claude directory, because an agent configuration is exactly
; the file where a tool writing without asking would be least forgivable. The after-install page
; prints the two commands and the user decides.
Source: "agent\SKILL.md"; DestDir: "{app}\agent"; Flags: ignoreversion
Source: "agent\settings-snippet.json"; DestDir: "{app}\agent"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Autostart, per-user and off unless it was asked for.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "DockerDesk"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: startupicon

; EnginePaths says putting the CLI folder on PATH is the installer's job, and this is it. HKCU, so
; no elevation; expandsz, because that is what Windows keeps Path as and rewriting it as a plain
; string would flatten every %VAR% already in it.
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; \
    ValueData: "{olddata};{app}\bin"; Tasks: pathentry; Check: PathEntryMissing

[Run]
; postinstall and checked by default: what the user just installed is an icon, and not starting it
; leaves them looking at nothing. skipifsilent, because an unattended install pushed to a machine
; must not make a tray icon appear in somebody's session — there is no self-update here that would
; need to relaunch itself silently, which is the one reason to leave that flag off.
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName} now"; \
    Flags: nowait postinstall skipifsilent

[Code]
const
  DistroName = 'dockerdesk';

// ---------------------------------------------------------------------------------------------
// PATH
// ---------------------------------------------------------------------------------------------

function PathEntryMissing: Boolean;
var
  Current: string;
begin
  // Idempotent: a reinstall must not append the same folder a second time. Semicolons on both ends
  // so \DockerDesk\bin is not matched inside \DockerDesk\bin2.
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Current) then
    Current := '';
  Result := Pos(';' + Lowercase(ExpandConstant('{app}\bin')) + ';',
                ';' + Lowercase(Current) + ';') = 0;
end;

procedure RemovePathEntry;
var
  Current, Wanted: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Current) then
    Exit;
  Wanted := ExpandConstant('{app}\bin');
  P := Pos(Lowercase(';' + Wanted), Lowercase(Current));
  if P > 0 then
    Delete(Current, P, Length(Wanted) + 1)
  else
  begin
    P := Pos(Lowercase(Wanted + ';'), Lowercase(Current));
    if P > 0 then
      Delete(Current, P, Length(Wanted) + 1)
    else
    begin
      P := Pos(Lowercase(Wanted), Lowercase(Current));
      if P = 0 then Exit;
      Delete(Current, P, Length(Wanted));
    end;
  end;
  RegWriteExpandStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Current);
end;

// ---------------------------------------------------------------------------------------------
// The preflight, run by the product rather than restated here
// ---------------------------------------------------------------------------------------------

procedure ShowPreflight;
var
  Code: Integer;
  ReportPath: string;
begin
  // The product's own preflight, not a second opinion written in Pascal: two reports about one
  // machine that read differently are two things for a user to learn. Redirected through cmd, which
  // is also the only form of a console verb an installer should use — a windowed executable hands its
  // output to whatever holds its standard handles, and here that is this file.
  //
  // Into {app} and not {tmp}: {tmp} is deleted when Setup exits, and this is a report somebody is
  // going to want in front of them while they change a BIOS setting or install a Windows feature.
  ReportPath := ExpandConstant('{app}\preflight.txt');
  if not Exec(ExpandConstant('{cmd}'),
              '/C ""' + ExpandConstant('{app}\{#MyAppExeName}') + '" --preflight > "'
              + ReportPath + '" 2>&1"',
              '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Exit;

  // Exit code 0 means every blocking row is green, and there is nothing to interrupt anybody with.
  if Code = 0 then
    Exit;

  // The report is written either way, and the dialog only happens when somebody is there to read it.
  // A modal box in an unattended install is a machine that looks hung to whoever deployed it.
  if WizardSilent then
    Exit;

  // The report itself is not pasted into this dialog. LoadStringFromFile reads AnsiString, the report
  // is UTF-8 with em dashes and arrows in every row, and a message box full of mojibake is worse than
  // no message box. The default viewer reads UTF-8 correctly, so it gets to show it.
  if MsgBox('DockerDesk is installed, but this machine cannot host the engine yet.' + #13#10#13#10
          + 'Nothing has been downloaded, and nothing is broken — the preflight found at least one '
          + 'row that blocks an install, and each one names the single action that changes it.'
          + #13#10#13#10
          + ReportPath + #13#10#13#10
          + 'Open it now?',
            mbInformation, MB_YESNO) = IDYES then
    ShellExec('open', ReportPath, '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    ShowPreflight;
end;

// ---------------------------------------------------------------------------------------------
// Uninstall: remove what was installed, ask about what was created
// ---------------------------------------------------------------------------------------------

function OwnedDataExists: Boolean;
begin
  // The distro directory is where the imported virtual disk lives, so its presence is the question.
  // Deliberately not asked by running `wsl -l`: wsl.exe writes UTF-16LE, which is the one decoding
  // wart this project has already been bitten by, and a misread here would delete on a maybe.
  Result := DirExists(ExpandConstant('{app}\distro'))
         or DirExists(ExpandConstant('{app}\downloads'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  RemovePathEntry;

  if not OwnedDataExists then
    Exit;

  // Silence means keep. An unattended uninstall must never be the thing that deletes somebody's
  // images and volumes, and there is nobody there to ask.
  if UninstallSilent then
    Exit;

  if MsgBox('Also delete the ' + DistroName + ' WSL2 distribution?' + #13#10#13#10
          + 'It holds every image, container and volume DockerDesk created, and there is no '
          + 'undo. Choosing No leaves it on disk, and reinstalling DockerDesk picks it up '
          + 'again.' + #13#10#13#10
          + ExpandConstant('{app}'),
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then
    Exit;

  // Unregister first: the virtual disk is open while the distribution is registered, so deleting the
  // directory underneath it fails and leaves a distribution pointing at nothing.
  Exec(ExpandConstant('{sys}\wsl.exe'), '--unregister ' + DistroName,
       '', SW_HIDE, ewWaitUntilTerminated, Code);
  DelTree(ExpandConstant('{app}\distro'), True, True, True);
  DelTree(ExpandConstant('{app}\downloads'), True, True, True);
  DelTree(ExpandConstant('{app}\bin'), True, True, True);
end;
