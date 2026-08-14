; Inno Setup script for FreeWilly (DD14).
;
; Build, from the repository root:
;   build\build-installer.cmd
; or by hand:
;   dotnet publish src\FreeWilly.Tray -c Release
;   "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" build\installer.iss
; Output: dist\FreeWilly-Setup.exe
;
; Every relative path below resolves against THIS file's directory (build\), so the ones pointing
; at the repository root are "..\"-relative.

#define MyAppName "FreeWilly"
#define MyAppPublisher "FreeWilly contributors"

; Still the old repository. Renaming it moves every published URL at once and is not this file's to
; do — DD59 waits on the GitHub rename itself.
#define MyAppUrl "https://github.com/alegauss/freewilly"

#define MyAppExeName "FreeWilly.exe"
#define MyPublishDir "..\src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish"

; Read straight off the published .exe, which got it from <Version> in Directory.Build.props. There
; is no second version to bump here, and a PackagingTests case holds that string to "x.y.z" with no
; commit suffix — Add/Remove Programs shows this verbatim. Requires the publish to have run first.
#define MyAppVersion GetStringFileInfo(MyPublishDir + "\" + MyAppExeName, PRODUCT_VERSION)

[Setup]
; Inno identifies a product by AppId and by nothing else, so from the first release onward THIS
; STRING NEVER CHANGES. Changing it is the one move that cannot be undone from here: the new setup
; would not see the old install, so a machine would carry two entries in Add/Remove Programs, two
; Run values and two roots — and the old uninstaller, run afterwards, offers to delete the engine
; root the new install is now using. It is also what makes an upgrade land where the previous
; install did, because Inno records the install directory against it.
;
; DD57 kept the previous id for exactly that reason and left the old product name visible inside it.
; DD86 changed it once, and could: nothing has been released, so there is no copy on any machine
; whose identity this is. That window closes with the first published tag.
AppId={{6B0E4D2A-9C77-4A31-8F5E-FREEWILLY0001}
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
;
; Inno records the directory against the AppId, so an upgrade reuses whatever the previous install
; chose and a fresh one gets this. DD57 weighed changing the AppId and did not: see the note over it.
DefaultDirName={localappdata}\FreeWilly
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
SetupIconFile=..\src\FreeWilly.Tray\FreeWilly.ico
OutputDir=..\dist
OutputBaseFilename=FreeWilly-Setup
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
;
; DD121: this covers the uninstall too, and it is the backstop rather than the plan. The uninstall
; asks the product to close itself first — a terminated tray leaves its icon in the notification area
; until something hovers it — and only what a graceful exit missed reaches Restart Manager.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start {#MyAppName} with Windows"; GroupDescription: "Startup:"

; DD119. Ticked by default, because an install that leaves the engine out is an install of nothing:
; `docker` is not a command, Start engine has no distribution to boot, and the only thing that
; changes either is a verb the wizard never named. Unticking leaves exactly that install, on purpose
; — a quarter of a gigabyte over somebody's tethered connection is theirs to decline, and
; `FreeWilly.exe --provision` does the same work later.
;
; Downloading is not starting. The engine is still not running when Setup closes, and no service is
; registered: a resident background service is a stated non-goal, and an installer that leaves a
; container engine running is the weight this project is an answer to.
Name: "engine"; Description: "Download and install the container engine (about 250 MB)"; \
    GroupDescription: "Container engine:"

Name: "pathentry"; Description: "Put docker and freewilly on my PATH"; GroupDescription: "Command line:"

[Files]
; One file. That is DD14: one .exe to publish, to sign, to install and to hand somebody.
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; DD24. The agent surface is reached as `freewilly read ...`, which is the literal string an
; allowlist entry matches - `Bash(freewilly read:*)`. The .exe lives in {app} and only {app}\bin is
; on PATH, so without this the one command the whole read/do split exists to make grantable does not
; resolve at all. A forwarder rather than a second PATH entry: one name on PATH, one thing to remove.
Source: "freewilly.cmd"; DestDir: "{app}\bin"; Flags: ignoreversion; Tasks: pathentry

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
; Autostart, per-user and off unless it was asked for. --tray is load-bearing: a bare launch opens
; the window (DD80), and a window in the face at every logon is exactly the regression that change
; could otherwise cause. This is the only caller that wants the tray on its own.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; \
    ValueData: """{app}\{#MyAppExeName}"" --tray"; \
    Flags: uninsdeletevalue; Tasks: startupicon

; The engine's own Run value, which this installer never writes — `freewilly --autostart on` does,
; and only if the user asks (DD97). It is named here so the uninstaller takes it: two settings mean
; two values, and leaving one behind is an entry pointing at an executable that has been deleted.
;
; ValueType: none is what makes that "delete on uninstall, touch nothing on install". Writing it
; here instead would turn the engine autostart ON for everyone, and off-by-default is not a
; preference in this product — it is the whole complaint about Docker Desktop.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: none; ValueName: "FreeWilly Engine"; \
    Flags: uninsdeletevalue

; EnginePaths says putting the CLI folder on PATH is the installer's job, and this is it. HKCU, so
; no elevation; expandsz, because that is what Windows keeps Path as and rewriting it as a plain
; string would flatten every %VAR% already in it.
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; \
    ValueData: "{olddata};{app}\bin"; Tasks: pathentry; Check: PathEntryMissing

; DOCKER_CONFIG, which is what makes `docker compose` a subcommand in the user's own shell (DD124).
; The plugins DD73 and DD74 place sit in {app}\cli-plugins, and the CLI looks for a plugin in
; $DOCKER_CONFIG\cli-plugins and nowhere else — so without this, an `ant` or a `make` driving the
; docker on PATH gets `unknown flag: --build` and every `docker build` is the legacy builder.
;
; Same Tasks: pathentry as the entry above, and that is the rule rather than a convenience: this
; variable is read by every docker.exe a shell runs and carries config.json, the contexts and the
; docker login credentials with it. Pointing it here is honest exactly when this install owns the
; docker command, which is what the PATH entry means. A user who declined that checkbox is left
; alone. DockerConfigEntry.Ensure applies the same rule at tray startup, for an install made before
; DD124 — this half is what makes a fresh install correct before the tray has ever run.
;
; uninsdeletevalue, because a value naming a directory the uninstaller deleted is worse than none:
; the next docker.exe on that machine would read a config directory that is not there.
Root: HKCU; Subkey: "Environment"; ValueType: string; ValueName: "DOCKER_CONFIG"; \
    ValueData: "{app}"; Tasks: pathentry; Flags: uninsdeletevalue

[Run]
; postinstall and checked by default: what the user just installed is an icon, and not starting it
; leaves them looking at nothing. skipifsilent, because an unattended install pushed to a machine
; must not make a tray icon appear in somebody's session — there is no self-update here that would
; need to relaunch itself silently, which is the one reason to leave that flag off.
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName} now"; \
    Flags: nowait postinstall skipifsilent

[Code]
const
  // The one distribution this product owns, spelled here and in EnginePaths.CurrentDistribution,
  // with a test holding the two equal. The uninstall unregisters it by name rather than deriving
  // which one is there: a derivation that got it wrong would leave a distribution no uninstaller
  // knows about.
  DistroName = 'freewilly';

  // Every step EngineProvisioner runs, so the bar below is a count rather than a guess. A test holds
  // this equal to ProvisioningStep's member count: a step added there and not here leaves a
  // successful install with a bar that never reaches the end, which reads as a failure.
  ProvisioningSteps = 11;

var
  // Built in InitializeWizard and shown from CurStepChanged, which is the only order Setup supports
  // — a custom page cannot be created once the wizard is running.
  ProvisionPage: TOutputProgressWizardPage;
  ProvisionLogPath: string;
  ProvisionStepsSeen: Integer;
  ProvisionLastLine: string;

  // DD123. The tasks page, drawn here rather than by Setup, and the four boxes on it.
  TasksPage: TWizardPage;
  WantDesktopIcon, WantStartupIcon, WantEngine, WantPathEntry: TNewCheckBox;

// ---------------------------------------------------------------------------------------------
// The tasks page, drawn rather than asked for (DD123)
// ---------------------------------------------------------------------------------------------
//
// Setup's own Select Additional Tasks page draws each box narrower than the glyph it holds: an
// unticked one is a vertical sliver and a ticked one a fragment of a check. Measured on Windows 11
// at 3840x2160, 200% scaling — and reproduced with Inno Setup 6.7.3's factory defaults and nothing
// of this script in them, so it is `TNewCheckListBox` and not anything here.
//
// Three of the four tasks are on by default, and one of them spends a quarter of a gigabyte of
// somebody's connection. A page where the reader cannot make out which boxes are ticked is not a
// page they can decline anything on, and dropping choices to dodge a drawing bug would trade an
// unreadable wizard for a decision taken on their behalf.
//
// So the page is rebuilt out of plain TNewCheckBox controls, which draw correctly at the same
// scaling — measured on this project's own uninstall page (DD121).
//
// [Tasks] STAYS, and that is what makes this small. It remains the source of truth: `Tasks:`
// parameters in [Files], [Icons] and [Registry] keep working untouched, `/MERGETASKS` keeps
// working for unattended installs, and a silent install never reaches this page at all. All this
// does is skip the broken page and hand Setup the same answer through WizardSelectTasks.

/// One of Setup's own messages, with the placeholders its own pages would have filled in.
function Message(const Id: string): string;
begin
  // SetupMessage hands back the raw string, and the standard messages carry [name] and [name/ver]
  // for the page that shows them to substitute. Setup's own pages do it; a page built here has to,
  // or the wizard reads "...while installing [name], then click Next" in every language shipped.
  // Measured on the page below before this existed.
  Result := Id;
  StringChange(Result, '[name/ver]', '{#MyAppName} {#MyAppVersion}');
  StringChange(Result, '[name]', '{#MyAppName}');
end;

/// Add one checkbox under a group heading, and answer the Y the next control starts at.
function TaskBox(Page: TWizardPage; const Group, Caption: string; Ticked: Boolean;
                 Top: Integer; var Box: TNewCheckBox): Integer;
var
  Heading: TNewStaticText;
begin
  Heading := TNewStaticText.Create(Page);
  Heading.Parent := Page.Surface;
  Heading.Left := 0;
  Heading.Top := Top;
  Heading.Caption := Group;

  Box := TNewCheckBox.Create(Page);
  Box.Parent := Page.Surface;

  // Width before Caption, and both after the control exists: the same ordering rule DD121 measured
  // on the uninstall page, where a caption assigned to a control that had not been given its width
  // wrapped at a column of zero.
  Box.Left := ScaleX(8);
  Box.Top := Heading.Top + Heading.Height + ScaleY(6);
  Box.Width := Page.SurfaceWidth - ScaleX(8);
  Box.Height := ScaleY(17);
  Box.Caption := Caption;
  Box.Checked := Ticked;

  Result := Box.Top + Box.Height + ScaleY(12);
end;

procedure BuildTasksPage;
var
  Intro: TNewStaticText;
  Y: Integer;
begin
  // Positioned after the directory page, which is exactly where Setup's own tasks page stands in
  // this wizard — there is no components page and DisableProgramGroupPage takes the other one.
  TasksPage := CreateCustomPage(
    wpSelectDir, SetupMessage(msgWizardSelectTasks), SetupMessage(msgSelectTasksDesc));

  Intro := TNewStaticText.Create(TasksPage);
  Intro.Parent := TasksPage.Surface;
  Intro.Left := 0;
  Intro.Top := 0;
  Intro.Width := TasksPage.SurfaceWidth;
  Intro.WordWrap := True;
  Intro.AutoSize := True;
  Intro.Caption := Message(SetupMessage(msgSelectTasksLabel2));
  Y := Intro.Top + Intro.Height + ScaleY(16);

  // The captions are Setup's own messages where Setup has one, so a translation this script never
  // wrote still reaches the page. A PackagingTests case holds each of these equal to the
  // [Tasks] description it stands in for — two spellings of one string is how a box ends up
  // promising something other than what ticking it does.
  // Which boxes start ticked, stated here because it cannot be asked for. WizardIsTaskSelected is
  // the obvious reader and it does not work from a page that replaces the one it reads: Setup fills
  // its task list while preparing wpSelectTasks, this skips that page, and the function answered
  // False for all four — measured, on this page, with every box coming up empty.
  //
  // So it is the same shape as DistroName and ProvisioningSteps elsewhere in this file: a value
  // restated in Pascal beside the section that owns it, with a PackagingTests case holding the two
  // equal. `Flags: unchecked` on the desktop icon and nothing on the other three is the fact, and
  // that test is what notices if either side moves. Getting it wrong is not theoretical — the
  // desktop icon came up ticked on the first build of this page.
  Y := TaskBox(TasksPage, ExpandConstant('{cm:AdditionalIcons}'),
       ExpandConstant('{cm:CreateDesktopIcon}'), False, Y, WantDesktopIcon);
  Y := TaskBox(TasksPage, 'Startup:',
       'Start {#MyAppName} with Windows', True, Y, WantStartupIcon);
  Y := TaskBox(TasksPage, 'Container engine:',
       'Download and install the container engine (about 250 MB)', True, Y, WantEngine);
  TaskBox(TasksPage, 'Command line:',
       'Put docker and freewilly on my PATH', True, Y, WantPathEntry);
end;

/// One task's term, named whether it was ticked or not.
function Term(const Name: string; Ticked: Boolean): string;
begin
  // Both directions, always. Naming only the ticked ones would leave a default standing through an
  // untick, and the default that costs somebody a quarter of a gigabyte is one of these four.
  if Ticked then
    Result := Name + ','
  else
    Result := '!' + Name + ',';
end;

/// Hand Setup the answer this page collected, in the spelling /MERGETASKS uses.
function ChosenTasks: string;
begin
  Result := Term('desktopicon', WantDesktopIcon.Checked)
          + Term('startupicon', WantStartupIcon.Checked)
          + Term('engine', WantEngine.Checked)
          + Term('pathentry', WantPathEntry.Checked);

  // The trailing comma an empty final term would otherwise leave.
  Result := Copy(Result, 1, Length(Result) - 1);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // The broken one, and only ever this one. Skipped rather than removed, because [Tasks] is still
  // what every `Tasks:` parameter and every /MERGETASKS reads.
  Result := PageID = wpSelectTasks;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = TasksPage.ID then
    WizardSelectTasks(ChosenTasks);
end;

procedure InitializeWizard;
begin
  ProvisionPage := CreateOutputProgressPage(
    'Container engine',
    'Setup is putting the engine on this machine. Nothing is started by this.');

  BuildTasksPage;
end;

// ---------------------------------------------------------------------------------------------
// PATH
// ---------------------------------------------------------------------------------------------

function PathEntryMissing: Boolean;
var
  Current: string;
begin
  // Idempotent: a reinstall must not append the same folder a second time. Semicolons on both ends
  // so \FreeWilly\bin is not matched inside \FreeWilly\bin2.
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

function RunPreflight: Boolean;
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
  begin
    // It never ran, so there is no verdict. Treated as a block: the caller's next move would be to
    // download a quarter of a gigabyte on the strength of a check that did not happen.
    Result := False;
    Exit;
  end;

  // Exit code 0 means every blocking row is green, and there is nothing to interrupt anybody with.
  Result := Code = 0;
  if Result then
    Exit;

  // The report is written either way, and the dialog only happens when somebody is there to read it.
  // A modal box in an unattended install is a machine that looks hung to whoever deployed it.
  if WizardSilent then
    Exit;

  // The report itself is not pasted into this dialog. LoadStringFromFile reads AnsiString, the report
  // is UTF-8 with em dashes and arrows in every row, and a message box full of mojibake is worse than
  // no message box. The default viewer reads UTF-8 correctly, so it gets to show it.
  if MsgBox('FreeWilly is installed, but this machine cannot host the engine yet.' + #13#10#13#10
          + 'Nothing has been downloaded, and nothing is broken — the preflight found at least one '
          + 'row that blocks an install, and each one names the single action that changes it.'
          + #13#10#13#10
          + ReportPath + #13#10#13#10
          + 'Open it now?',
            mbInformation, MB_YESNO) = IDYES then
    ShellExec('open', ReportPath, '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

// ---------------------------------------------------------------------------------------------
// The engine itself (DD119)
// ---------------------------------------------------------------------------------------------

procedure ProvisionSaid(const S: String; const Error, FirstLine: Boolean);
var
  Line: string;
begin
  Line := Trim(S);
  if Line = '' then
    Exit;

  // Kept whatever happens, and beside the preflight report for the same reason: this is the file
  // somebody opens after Setup has closed, so {tmp} — which Setup deletes on its way out — is the
  // one place it must not be.
  SaveStringToFile(ProvisionLogPath, Line + #13#10, True);

  // `--provision` prints exactly one of these per step, pass or fail, and nothing else it writes
  // opens with either. Counting them is what moves the bar.
  if (Pos('[ok  ]', Line) = 1) or (Pos('[FAIL]', Line) = 1) then
  begin
    ProvisionStepsSeen := ProvisionStepsSeen + 1;
    ProvisionPage.SetProgress(ProvisionStepsSeen, ProvisioningSteps);
    ProvisionLastLine := Line;
  end;

  // The step line goes on the page, so a run that stops leaves the step it stopped at on screen
  // rather than a bar that has already moved past it.
  ProvisionPage.SetText(
    'Downloading and installing the engine. This can take several minutes.', ProvisionLastLine);
end;

function ProvisionEngine: Boolean;
var
  Code: Integer;
  Ran: Boolean;
begin
  ProvisionStepsSeen := 0;
  ProvisionLastLine := '';
  ProvisionLogPath := ExpandConstant('{app}\provision.log');

  // Truncated rather than appended to: a reinstall's log is about this run, and two runs in one
  // file is a reader guessing which failure is the live one.
  DeleteFile(ProvisionLogPath);

  ProvisionPage.SetText('Contacting dl-cdn.alpinelinux.org and download.docker.com.', '');
  ProvisionPage.SetProgress(0, ProvisioningSteps);
  ProvisionPage.Show;
  try
    // ExecAndLogOutput and not Exec: the child's output is what fills the page above, a line at a
    // time as each step lands. The working directory is {app} so a relative path in any error the
    // verb prints resolves where the reader would look for it.
    Ran := ExecAndLogOutput(
      ExpandConstant('{app}\{#MyAppExeName}'), '--provision', ExpandConstant('{app}'),
      SW_HIDE, ewWaitUntilTerminated, Code, @ProvisionSaid);
  finally
    ProvisionPage.Hide;
  end;

  Result := Ran and (Code = 0);
  if Result or WizardSilent then
    Exit;

  if MsgBox('FreeWilly is installed, but the engine is not.' + #13#10#13#10
          + 'This machine passed the preflight, so the download or the WSL2 import is what '
          + 'stopped — a connection that dropped, a proxy, or a checksum that did not match. '
          + 'Nothing is half-installed: every step is repeatable, and what is already verified '
          + 'on disk is not fetched again.' + #13#10#13#10
          + 'To try again, open a terminal and run:' + #13#10
          + '    freewilly --provision' + #13#10#13#10
          + 'The step it stopped at is the last line of:' + #13#10
          + ProvisionLogPath + #13#10#13#10
          + 'Open it now?',
            mbError, MB_YESNO) = IDYES then
    ShellExec('open', ProvisionLogPath, '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  // The order is the argument. The preflight decides whether this machine can host an engine at
  // all, and unpacking one onto a machine that cannot is the failure it exists to prevent — so a
  // red row skips the download rather than spending it. `RunPreflight` has already shown the user
  // its report by the time it answers False.
  if not RunPreflight then
    Exit;

  if WizardIsTaskSelected('engine') then
    ProvisionEngine;
end;

// ---------------------------------------------------------------------------------------------
// Uninstall: stop what is running before deleting it (DD121)
// ---------------------------------------------------------------------------------------------
//
// An uninstall that cannot delete the program it is uninstalling is not an uninstall. The tray holds
// {app}\FreeWilly.exe open, so what used to happen was: the Run value went, the PATH entry went, the
// Add/Remove Programs entry went, and then the one file could not be deleted — leaving a root nobody
// owns and no uninstaller left to offer to take it.
//
// Four steps, in this order. Ask, stop gracefully, force only what did not go, then delete and say
// what could not be removed rather than exiting 0 over it.

var
  // What the page below decided, read again in usPostUninstall — after Inno has removed its own
  // files, which is the only point at which the root is otherwise empty. Silence means keep, so the
  // safe default is what an unattended uninstall gets by never touching this.
  RemoveTheDistribution: Boolean;

  // Set by the tasklist callback, because a callback cannot return anything.
  SawTheProcess: Boolean;

function OwnedDataExists: Boolean;
begin
  // The distro directory is where the imported virtual disk lives, so its presence is the question.
  // Deliberately not asked by running `wsl -l`: wsl.exe writes UTF-16LE, which is the one decoding
  // wart this project has already been bitten by, and a misread here would delete on a maybe.
  Result := DirExists(ExpandConstant('{app}\distro'))
         or DirExists(ExpandConstant('{app}\downloads'));
end;

procedure TasklistSaid(const S: String; const Error, FirstLine: Boolean);
begin
  if Pos(Lowercase('{#MyAppExeName}'), Lowercase(S)) > 0 then
    SawTheProcess := True;
end;

function AnythingIsStillRunning: Boolean;
var
  Code: Integer;
begin
  // tasklist rather than a handle test: a running executable can still be renamed and can still be
  // opened for reading, so every cheap probe answers the wrong question. With no match it prints
  // "INFO: No tasks are running..." and the image name appears nowhere in it, which is the whole
  // decision below.
  SawTheProcess := False;
  if not ExecAndLogOutput(ExpandConstant('{sys}\tasklist.exe'),
       '/FI "IMAGENAME eq {#MyAppExeName}" /NH', '', SW_HIDE,
       ewWaitUntilTerminated, Code, @TasklistSaid) then
  begin
    // It could not be asked. Answering yes here would force-kill on no evidence, so this defers to
    // Restart Manager, which is exactly what CloseApplications is carried for.
    Result := False;
    Exit;
  end;

  Result := SawTheProcess;
end;

/// Lay one wrapping paragraph out at Y and answer the Y the next control starts at.
function Paragraph(Form: TSetupForm; const Text: string; Left, Top, Width: Integer): Integer;
var
  Block: TLabel;
begin
  Block := TLabel.Create(Form);
  Block.Parent := Form;

  // AutoSize with WordWrap is what makes the height follow the text rather than the other way
  // round. A fixed height would be a guess about a font this script does not choose and a language
  // it does not know: BrazilianPortuguese.isl is shipped beside Default.isl, and the first
  // translation of this page that runs one line longer would have that line clipped off the bottom
  // of a fixed box with nothing to say it had happened.
  // The order is the whole of it, and both ways of getting it wrong were measured on this page.
  //
  // An auto-sizing label re-measures itself when its CAPTION changes and not when its width does,
  // and it measures at whatever width it has at that moment. So the width has to be right before
  // the text arrives, and these two flags have to be set before the width — an auto-sizing label
  // with no text in it collapses to nothing, and a flag flipped after the width would collapse a
  // width that had already been set.
  //
  // Setting the flags first, then the width, then the caption is the one order that leaves both
  // the wrap column and the height correct. Caption before width wraps the text at a column of
  // zero: one word per line, a page tall, and a form dragged out to the size of the screen. Caption
  // before width but width set afterwards is worse, because it looks fixed — the text re-wraps to
  // the right column, and the height stays at the value the collapsed measurement produced, so the
  // page renders correctly above a screenful of blank space.
  Block.WordWrap := True;
  Block.AutoSize := True;
  Block.Left := Left;
  Block.Top := Top;
  Block.Width := Width;
  Block.Caption := Text;

  Result := Block.Top + Block.Height;
end;

function AskAboutTheUninstall: Boolean;
var
  Form: TSetupForm;
  Heading: TLabel;
  Distribution: TNewCheckBox;
  Proceed, Abandon: TNewButton;
  Left, Width, Y, Buttons: Integer;
begin
  // Setup's wizard-page API is Setup's alone, so an uninstaller that wants a page builds the form.
  // It is worth the lines: the two things about to happen — a running program closed, and possibly
  // every image and volume deleted — are precisely the two a MsgBox chain asks about one at a time,
  // out of order, with no way to see them together before agreeing to either.
  //
  // Fixed width and not resizable: the prose is written to a column, and a form somebody could drag
  // wider would only re-wrap it. The height is set at the end, from what the text actually measured.
  Left := ScaleX(16);
  Width := ScaleX(398);

  Form := CreateCustomForm(Width + (2 * Left), ScaleY(260), False, False);
  try
    Form.Caption := 'Uninstall {#MyAppName}';

    Heading := TLabel.Create(Form);
    Heading.Parent := Form;
    Heading.Left := Left;
    Heading.Top := ScaleY(16);
    Heading.AutoSize := True;
    Heading.Font.Style := [fsBold];
    Heading.Caption := 'These are closed before anything is removed';
    Y := Heading.Top + Heading.Height + ScaleY(10);

    Y := Paragraph(Form,
        'The tray icon and window, asked to close themselves.'#13#10
      + 'The container engine, and the ' + DistroName + ' distribution it runs in.'#13#10#13#10
      + 'Anything still holding a file after that is closed forcibly. Windows will not delete a '
      + 'program that is running, and an uninstall that stops there leaves a folder nothing can '
      + 'remove.', Left, Y, Width) + ScaleY(16);

    Distribution := TNewCheckBox.Create(Form);
    Distribution.Parent := Form;
    Distribution.SetBounds(Left, Y, Width, ScaleY(17));
    Distribution.Caption := 'Also delete the WSL2 distribution';

    // Off, and it stays off in every path that does not include somebody reading this. Stopping is
    // reversible; this is the one question here with no undo.
    Distribution.Checked := False;
    Distribution.Enabled := OwnedDataExists;
    Y := Distribution.Top + Distribution.Height + ScaleY(4);

    // Indented under the box it qualifies, so the sentence with no undo in it is read as part of
    // the tick rather than as a second, separate thing.
    if OwnedDataExists then
      Y := Paragraph(Form,
          'It holds every image, container and volume FreeWilly created, and there is no undo. '
        + 'Left alone it stays on disk, and reinstalling FreeWilly picks it up again.'#13#10
        + ExpandConstant('{app}'), Left + ScaleX(16), Y, Width - ScaleX(16))
    else
      Y := Paragraph(Form,
          'There is none on this machine — nothing was ever provisioned here.',
          Left + ScaleX(16), Y, Width - ScaleX(16));

    Buttons := Y + ScaleY(20);

    // Positioned against the text column rather than against Form.ClientWidth, which is not the
    // width this page asked for until the assignment below has actually happened.
    Proceed := TNewButton.Create(Form);
    Proceed.Parent := Form;
    Proceed.SetBounds(Left + Width - ScaleX(75 + 6 + 75), Buttons, ScaleX(75), ScaleY(23));
    Proceed.Caption := 'Remove';
    Proceed.ModalResult := mrOk;
    Proceed.Default := True;

    Abandon := TNewButton.Create(Form);
    Abandon.Parent := Form;
    Abandon.SetBounds(Left + Width - ScaleX(75), Buttons, ScaleX(75), ScaleY(23));
    Abandon.Caption := 'Cancel';
    Abandon.ModalResult := mrCancel;
    Abandon.Cancel := True;

    // Now that everything has measured itself. A form sized before its text is a form with either
    // a clipped last line or a band of empty space under the buttons, depending on the machine.
    // The width is restated rather than trusted: it is the one number every control above was
    // placed against, so this is where the two are held equal.
    Form.ClientWidth := Width + (2 * Left);
    Form.ClientHeight := Buttons + ScaleY(23 + 16);
    Form.ActiveControl := Proceed;

    // Centred on the screen by CreateCustomForm itself. There is no WizardForm to centre on in an
    // uninstall, and the progress window behind this one is Inno's rather than a page of ours.
    Result := Form.ShowModal = mrOk;
    if Result then
      RemoveTheDistribution := Distribution.Checked;
  finally
    Form.Free;
  end;
end;

procedure StopEverything;
var
  Code: Integer;
  Exe: string;
begin
  Exe := ExpandConstant('{app}\{#MyAppExeName}');

  // Nothing to ask, and nothing that could have started the engine either. A root without the
  // executable in it is one a previous uninstall got most of the way through.
  if not FileExists(Exe) then
    Exit;

  // The engine first. --stop ends the pipe relay and terminates the distribution, which is what
  // makes the unregister below a clean one — an open virtual disk is a directory that survives its
  // own deletion. It also takes the detached `--run` process with it, since what that serves lives
  // inside the distribution being terminated.
  Exec(Exe, '--stop', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code);

  // Then the tray, by the verb written for this (DD121). It waits until the process is actually
  // gone rather than until the request was delivered, so returning from here means the file is no
  // longer open. A kill would leave the notification icon in the overflow until something hovers it.
  Exec(Exe, '--quit', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code);

  if not AnythingIsStillRunning then
    Exit;

  // The last resort, and the page said it would happen. Reached by a process that ignored the verb
  // or by a `--run` started outside the tray; either way the alternative is the failure this whole
  // section exists to remove.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T',
       '', SW_HIDE, ewWaitUntilTerminated, Code);

  // Handles are closed by the kernel after the process is, and Inno's first delete follows
  // immediately. Half a second costs nothing on the path where it was not needed.
  Sleep(500);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
begin
  // Before Inno removes a single file, and after the user has already confirmed the uninstall
  // itself: the page below asks the two questions that confirmation could not, and Cancel on it
  // abandons the whole uninstall rather than leaving a half-removed install behind.
  if CurUninstallStep = usUninstall then
  begin
    // An unattended uninstall gets the safe half: everything is stopped, and nothing anybody owns
    // is deleted. A modal box in a deployment is a machine that looks hung to whoever pushed it.
    if not UninstallSilent then
    begin
      if not AskAboutTheUninstall then
        Abort;
    end;

    StopEverything;

    RemovePathEntry;

    // Everything this install put in {app} that Inno did not, and therefore does not know to take
    // back: the two reports, the CLI the provision extracted, and the plugin directory it filled.
    // None of it is anybody's data — it is this product's own files under this product's own root.
    // DD119 made that universal by provisioning during the install, so it is settled here rather
    // than left to the question above, which is about images and volumes and nothing else.
    DeleteFile(ExpandConstant('{app}\preflight.txt'));
    DeleteFile(ExpandConstant('{app}\provision.log'));
    DelTree(ExpandConstant('{app}\bin'), True, True, True);
    DelTree(ExpandConstant('{app}\cli-plugins'), True, True, True);

    if RemoveTheDistribution then
    begin
      // Terminated again rather than trusting the stop above: this is the one operation here that
      // cannot be repeated, and the executable that would have run --stop may not have been there.
      Exec(ExpandConstant('{sys}\wsl.exe'), '--terminate ' + DistroName,
           '', SW_HIDE, ewWaitUntilTerminated, Code);

      // Unregister first: the virtual disk is open while the distribution is registered, so
      // deleting the directory underneath it fails and leaves a distribution pointing at nothing.
      Exec(ExpandConstant('{sys}\wsl.exe'), '--unregister ' + DistroName,
           '', SW_HIDE, ewWaitUntilTerminated, Code);
      DelTree(ExpandConstant('{app}\distro'), True, True, True);
      DelTree(ExpandConstant('{app}\downloads'), True, True, True);
    end;

    Exit;
  end;

  if CurUninstallStep <> usPostUninstall then
    Exit;

  // Everything above happens in usUninstall, and that placement is the whole of this paragraph.
  //
  // Inno removes the install directory during its own file pass, which runs between usUninstall and
  // usPostUninstall, and it removes it only if it is empty by then. Doing this work in
  // usPostUninstall — where it stood — meant Inno met a root still holding distro\ and downloads\,
  // left it alone as it should, and then this emptied it a moment too late. Measured: an uninstall
  // that removed the registration, the distribution, the virtual disk and every file, and left an
  // empty C:\Users\...\FreeWilly standing with nothing left that would ever offer to take it.
  //
  // So the only thing left for this step is to check the work, and it cannot check the root: the
  // uninstaller is still running out of it, unins000.exe is still in it, and Inno deletes both
  // after this returns. What it can check is what the page promised — that the distribution and its
  // downloads are gone — and that is the promise with no undo behind it.
  if UninstallSilent or not RemoveTheDistribution or not OwnedDataExists then
    Exit;

  MsgBox('FreeWilly is uninstalled, but part of the WSL2 distribution could not be deleted:'
       + #13#10#13#10
       + ExpandConstant('{app}') + #13#10#13#10
       + 'Something was still holding a file inside it — most often WSL2 itself, which keeps the '
       + 'virtual disk open for a while after a distribution is unregistered. Running '
       + '"wsl --shutdown" and then deleting the folder by hand finishes it. Nothing here is '
       + 'registered any more, and nothing will start it again.',
         mbInformation, MB_OK);
end;
