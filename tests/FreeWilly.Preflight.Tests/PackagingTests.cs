using System.Reflection;
using FreeWilly.Core.Licensing;
using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The chain a release travels along (DD14): one version, stated once, and one file name.
/// </summary>
/// <remarks>
/// Every link here is a string that has to agree with a string somewhere else, and none of them
/// fails at compile time. The version is set in Directory.Build.props, compiled into the assembly,
/// read back off the published .exe by build\installer.iss, and shown by Windows in Add/Remove
/// Programs; the file name is set by AssemblyName, typed by a person, and written into the
/// installer's shortcuts. A break anywhere along either is found by running an installer, which is
/// the most expensive place to find anything.
/// </remarks>
public sealed class PackagingTests
{
    private static readonly Assembly Shipped = typeof(CommandLine).Assembly;

    [Fact]
    public void The_name_the_help_prints_is_the_name_the_build_produces()
    {
        // AssemblyName in FreeWilly.Tray.csproj, against the name every message and the installer
        // spell out. The assembly is a .dll here and an .exe when published, so compare the stem.
        var built = System.IO.Path.GetFileNameWithoutExtension(Shipped.Location);

        Assert.Equal(
            built,
            System.IO.Path.GetFileNameWithoutExtension(CommandLine.ExecutableName));
        Assert.Equal(".exe", System.IO.Path.GetExtension(CommandLine.ExecutableName));
    }

    [Fact]
    public void The_product_version_carries_no_commit_suffix()
    {
        // IncludeSourceRevisionInInformationalVersion=false in Directory.Build.props. Without it the
        // SDK appends "+<commit>", installer.iss reads that whole string out of the .exe with
        // GetStringFileInfo, and the version Windows shows in Add/Remove Programs has a git hash in
        // it. BuildVersion trims the suffix for its own display, so it cannot catch this.
        var informational = Shipped
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.NotNull(informational);
        Assert.DoesNotContain('+', informational);
    }

    [Fact]
    public void The_version_is_one_a_person_can_read_out_of_a_bug_report()
    {
        var current = BuildVersion.Current;

        Assert.NotEqual("0.0.0", current);
        Assert.True(
            Version.TryParse(current, out var parsed),
            $"{current} should parse as a version");
        Assert.Equal(current, parsed!.ToString());
    }

    /// <summary>The repository, found by walking up from the test binaries.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return directory!.FullName;
    }

    /// <summary>The installer script, read as text — the only way to assert on Inno's decisions.</summary>
    private static string InstallerScript() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "build", "installer.iss"));

    /// <summary>A workflow, read as text.</summary>
    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", name));

    /// <summary>The committed agent configuration, parsed.</summary>
    private static System.Text.Json.JsonElement Settings() =>
        System.Text.Json.JsonDocument
            .Parse(File.ReadAllBytes(Path.Combine(RepositoryRoot(), ".claude", "settings.json")))
            .RootElement;

    [Fact]
    public void The_committed_settings_still_grant_what_this_project_cannot_work_without()
    {
        // DD115. This file is committed on purpose — it grants this project's tools and wires the
        // roadkeep guard, so a clone works rather than prompting on every call — and it is rewritten
        // by whatever session happens to be open. Twice in one session fifteen entries vanished from
        // `allow` with nothing said, and the second time the deletion rode into a commit about
        // something else, because run-commit.cmd stages everything by design.
        //
        // A floor and not the whole list: pinning every entry would fail the build on a legitimate
        // addition, which is how a guard gets deleted. These are the ones whose loss costs
        // something — the tools this project's own loop is built on, and the two that let the
        // vendored roadkeep be called at all.
        var allowed = Settings()
            .GetProperty("permissions").GetProperty("allow")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .ToHashSet(StringComparer.Ordinal);

        string[] floor =
        [
            "Bash", "PowerShell", "Read", "Edit", "Write", "Glob", "Grep", "Skill", "Task",
            "mcp__roadkeep",
            "Bash(python .roadkeep/scripts/roadkeep.py:*)",
        ];

        foreach (var entry in floor)
        {
            Assert.True(
                allowed.Contains(entry),
                $".claude/settings.json no longer grants {entry}: a clone starts asking permission "
                + "for a tool this project had already granted, and nothing else would say so");
        }
    }

    [Fact]
    public void The_committed_settings_still_wire_the_guard_that_owns_the_governed_files()
    {
        // The consequential half of DD115, and the reason a floor over `allow` alone would not be
        // enough. roadkeep denies a hand-edit of ROADMAP, CHANGELOG and IMPROVEMENTS through this
        // hook; without it those files become ordinary text and the whole discipline this repository
        // runs on stops being enforced — silently, because nothing fails when a guard is absent.
        var hooks = Settings().GetProperty("hooks");

        foreach (var stage in new[] { "SessionStart", "PreToolUse", "Stop" })
        {
            var wired = hooks.GetProperty(stage).EnumerateArray()
                .SelectMany(group => group.GetProperty("hooks").EnumerateArray())
                .Select(hook => hook.GetProperty("command").GetString() ?? "")
                .ToList();

            Assert.True(
                wired.Exists(command => command.Contains("roadkeep-launch.py", StringComparison.Ordinal)
                    && command.Contains("guard", StringComparison.Ordinal)),
                $"no roadkeep guard is wired on {stage}, so a hand-edit of the governed files is "
                + "no longer refused");
        }

        // And the engine that guard reaches is the vendored one, which is the whole point of
        // carrying a copy: a checkout beside this repository is somebody else's working tree, and
        // mid-refactor it does not import at all.
        Assert.Equal(
            "${CLAUDE_PROJECT_DIR}/.roadkeep",
            Settings().GetProperty("env").GetProperty("ROADKEEP_HOME").GetString());
    }

    [Fact]
    public void The_gate_runs_the_vendored_engine_beside_the_one_it_floats_on()
    {
        // DD118. Two roadkeeps are in play here and they are allowed to differ: the workflow's
        // action floats on `main`, and DD116 made every hook run the copy vendored at `.roadkeep`.
        // One gates and one writes. A rule `main` gained and the vendored copy has not is a file
        // that lints clean on a developer's machine and red in CI — so both engines answer the same
        // question on every push, and the step states both versions rather than leaving "how far
        // behind" to somebody who thinks to ask.
        //
        // No pin is possible and that is why this is a read: roadkeep publishes no tags, and the
        // vendored copy reports its revision as `untracked` because it is a copy and not a checkout.
        var gate = File.ReadAllText(
            Path.Combine(RepositoryRoot(), ".github", "workflows", "roadkeep.yml"));

        Assert.Contains("alegauss/roadkeep@", gate, StringComparison.Ordinal);
        Assert.Contains(".roadkeep/scripts/roadkeep.py lint", gate, StringComparison.Ordinal);
        Assert.Contains(".roadkeep/scripts/roadkeep.py --version", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void The_engine_the_hooks_reach_is_the_one_this_repository_vendors()
    {
        // DD116, and it is asserted by running the launcher's own resolution rather than by reading
        // it: the defect was invisible in the file. `settings.json` sets ROADKEEP_HOME to
        // `${CLAUDE_PROJECT_DIR}/.roadkeep`, the harness passes env values through verbatim, and the
        // first candidate therefore never existed — so every hook fell through to a sibling
        // checkout, which is the neighbour's working tree that vendoring exists to stop depending
        // on. Earlier in this project's history that tree was mid-refactor and did not import, and
        // the guard denying hand-edits of the governed files was running a traceback.
        var root = RepositoryRoot();
        var launcher = Path.Combine(root, ".claude", "hooks", "roadkeep-launch.py");
        Assert.True(File.Exists(launcher), $"{launcher} is missing, so no hook can reach an engine");

        var probe = Path.Combine(Path.GetTempPath(), $"roadkeep-resolve-{Guid.NewGuid():N}.py");
        File.WriteAllText(
            probe,
            $"""
            import importlib.util
            spec = importlib.util.spec_from_file_location("rl", r"{launcher}")
            m = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(m)
            print(m._resolve())
            """);

        try
        {
            var run = new System.Diagnostics.ProcessStartInfo("python", probe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            };

            // Exactly what settings.json hands the hooks, so the test asks the question the harness
            // asks and not an easier one.
            run.Environment["CLAUDE_PROJECT_DIR"] = root;
            run.Environment["ROADKEEP_HOME"] = "${CLAUDE_PROJECT_DIR}/.roadkeep";

            // Importing the launcher makes Python write a .pyc beside it, inside the repository, on
            // every run of this test — and run-commit.cmd stages everything by design, so the first
            // one landed in a commit. A test must not leave anything behind in the tree it reads.
            run.Environment["PYTHONDONTWRITEBYTECODE"] = "1";

            using var process = System.Diagnostics.Process.Start(run);
            Assert.NotNull(process);
            var resolved = process!.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(30000);

            Assert.Equal(
                Path.Combine(root, ".roadkeep", "scripts", "roadkeep.py"),
                resolved);
        }
        finally
        {
            File.Delete(probe);
        }
    }

    /// <summary>Every script and workflow that could invoke the build, found rather than listed.</summary>
    private static IEnumerable<string> BuildScripts() =>
        new[] { "build", ".github/workflows", "scripts" }
            .Select(folder => Path.Combine(RepositoryRoot(), folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(file => Path.GetExtension(file) is ".cmd" or ".yml" or ".ps1");

    [Fact]
    public void No_build_command_names_the_folder_a_project_is_in()
    {
        // DD109. An interrupted WPF build leaves a <name>_<random>_wpftmp.csproj beside the project,
        // and MSBuild answers a folder holding two projects with MSB1050 — before it has evaluated
        // anything, so no target inside the project can prevent it. A command that has already
        // chosen its project cannot raise it at all, which closes the class rather than the case.
        //
        // Derived and never listed: a fourth script added next year is under this rule with no edit
        // here, which is the property the help-text guard was written for after two verbs went
        // missing unnoticed.
        var checkedAny = false;
        foreach (var script in BuildScripts())
        {
            foreach (var line in File.ReadAllLines(script))
            {
                if (!line.Contains("dotnet publish", StringComparison.Ordinal)
                    && !line.Contains("dotnet build", StringComparison.Ordinal))
                {
                    continue;
                }

                // A bare `dotnet build` takes the solution, which names its projects and cannot be
                // ambiguous. Only a line that points somewhere under src\ is making the choice.
                if (!line.Contains("src", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.True(
                    line.Contains(".csproj", StringComparison.Ordinal),
                    $"{Path.GetFileName(script)} names a folder rather than a project file, so a "
                    + $"leftover _wpftmp.csproj stops it with MSB1050:{Environment.NewLine}{line.Trim()}");
                checkedAny = true;
            }
        }

        Assert.True(checkedAny, "no build command was checked, so this guard proved nothing");
    }

    [Fact]
    public void The_stale_artefact_is_cleared_before_the_publish_it_would_stop()
    {
        // The sharper half of DD109. build.cmd already deleted the artefact — afterwards, to tidy
        // up — so the one script that owns the cleanup failed on exactly the run the cleanup exists
        // for and worked the second time, which is what made it read as flaky rather than as a rule.
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "build", "build.cmd"));

        var cleared = script.IndexOf("_wpftmp.csproj", StringComparison.Ordinal);
        var published = script.IndexOf("dotnet publish", StringComparison.Ordinal);

        Assert.True(cleared >= 0, "build.cmd no longer clears the stale _wpftmp.csproj at all");
        Assert.True(published >= 0, "build.cmd no longer publishes");
        Assert.True(
            cleared < published,
            "build.cmd clears the stale _wpftmp.csproj only after the publish that it stops");
    }

    [Fact]
    public void The_ordinary_path_compiles_the_installer_script_and_not_only_the_release()
    {
        // DD102, and the guard is over the workflow because that is where the defect was. Every
        // other test in this class asserts over the script as *text* — that a line says
        // `ValueType: none`, that an AppId is spelled a certain way — which proves the file says
        // what the author meant and can say nothing about whether Inno accepts it. DD97 shipped an
        // Inno construct on exactly that evidence, and the first reader of the file was the release,
        // by which point the tag was pushed.
        //
        // Asserted here rather than trusted to a reviewer, and for DD88's reason: the site build was
        // broken for 21 commits because its workflow was `workflow_dispatch` only, and nothing
        // noticed. A step deleted from check.yml would restore exactly that state, silently.
        var check = Workflow("check.yml");

        Assert.Contains("./.github/actions/inno-setup", check, StringComparison.Ordinal);
        Assert.Contains(@"build\installer.iss", check, StringComparison.Ordinal);

        // On every push and pull request, with no path filter in front of it. A conditional reader
        // of this file is the failure being repaired, not a cheaper version of the fix.
        Assert.Contains("on: [push, pull_request]", check, StringComparison.Ordinal);
        Assert.DoesNotContain("paths:", check, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_workflows_find_the_compiler_the_same_way()
    {
        // One definition, because there are two callers and they are one rule — the same reasoning
        // as Wsl.HasDriveLetter. Two inline probes would be two chances to disagree about where Inno
        // Setup lives, and the one that disagreed would be found by a release.
        foreach (var name in new[] { "check.yml", "release.yml" })
        {
            var workflow = Workflow(name);
            Assert.Contains("./.github/actions/inno-setup", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Inno Setup 6\\ISCC.exe",
                workflow);
        }
    }

    [Fact]
    public void The_AppId_is_pinned_here_so_a_future_tidy_cannot_move_it()
    {
        // Inno identifies a product by AppId and by nothing else, so from the first release onward
        // this is an identity rather than a spelling. Change it and a machine carrying the published
        // build ends up with two entries in Add/Remove Programs, two Run values and two roots — and
        // the old uninstaller then offers to delete the engine root the new install is using.
        //
        // DD86 changed it once, which was only free because nothing has been released. The literal
        // is repeated here rather than read from the script so that the next change has to be
        // deliberate in two files: this test is the record that it is not a name to sweep.
        Assert.Contains(
            "AppId={{6B0E4D2A-9C77-4A31-8F5E-FREEWILLY0001}",
            InstallerScript(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_tray_and_the_engine_start_at_logon_under_names_that_differ()
    {
        // DD97, and the assertion is inverted from what stood here. It used to require the two to
        // be spelled the SAME, reasoning that one value keeps the window and the uninstaller from
        // touching two entries. That holds for one feature and is exactly backwards for two: the
        // installer's checkbox writes `--tray` and `--autostart on` wrote `--run` over it, so
        // whichever ran last won. Ticking the box then turning the engine on stopped the tray
        // appearing, and turning the engine off deleted the box's entry outright.
        var script = InstallerScript();

        Assert.DoesNotContain("LegacyRunValue", script, StringComparison.Ordinal);
        Assert.NotEqual(
            Core.Engine.Autostart.TrayEntryName,
            Core.Engine.Autostart.EngineEntryName);

        // Each name is spelled in both files, which is the drift nothing else would notice.
        Assert.Contains(
            $"ValueName: \"{Core.Engine.Autostart.EngineEntryName}\"",
            script,
            StringComparison.Ordinal);
        Assert.Equal("FreeWilly", Core.Engine.Autostart.TrayEntryName);
    }

    [Fact]
    public void The_uninstaller_takes_the_engine_entry_without_the_installer_ever_writing_it()
    {
        // Two settings mean two values, and only one of them is the installer's to create. Leaving
        // the other behind is a Run entry pointing at an executable that has been deleted — the
        // exact state DD57 had to clean up once already.
        //
        // `ValueType: none` is what makes it delete-on-uninstall and touch nothing on install.
        // Writing it properly would turn the engine autostart on for everyone, and off-by-default
        // is not a preference here: it is the complaint about Docker Desktop that this answers.
        var engineValue = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.Contains(
                $"ValueName: \"{Core.Engine.Autostart.EngineEntryName}\"", StringComparison.Ordinal));

        Assert.Contains("ValueType: none", engineValue, StringComparison.Ordinal);
        Assert.DoesNotContain("ValueData:", engineValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_the_engine_autostart_off_leaves_the_trays_entry_alone()
    {
        // The failure itself, driven rather than restated. `Disable` removes rather than blanks,
        // deliberately, so before DD97 turning the engine off silently undid a box somebody had
        // ticked in the installer — and turning it ON overwrote it with `--run`, so the tray
        // stopped appearing at logon.
        //
        // A scratch key, never the real Run: this writes and deletes, and the machine running the
        // suite must not be what it experiments on.
        var scratch = $@"Software\FreeWilly\Tests\{Guid.NewGuid():N}";
        try
        {
            var tray = new Core.Engine.Autostart(
                @"""C:\x\FreeWilly.exe"" --tray", Core.Engine.Autostart.TrayEntryName, scratch);
            var engine = new Core.Engine.Autostart(
                @"""C:\x\FreeWilly.exe"" --run", Core.Engine.Autostart.EngineEntryName, scratch);

            tray.Enable();
            engine.Enable();

            // Both, at once, which is the state that was unreachable before: one value could only
            // hold one of the two commands.
            Assert.True(tray.Enabled);
            Assert.True(engine.Enabled);
            Assert.EndsWith("--tray", tray.Registered!, StringComparison.Ordinal);
            Assert.EndsWith("--run", engine.Registered!, StringComparison.Ordinal);

            engine.Disable();

            Assert.False(engine.Enabled);
            Assert.True(tray.Enabled, "turning the engine off took the tray's entry with it");
            Assert.EndsWith("--tray", tray.Registered!, StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(scratch, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Start_with_windows_asks_for_the_tray_alone()
    {
        // DD80 inverted the default: a bare launch opens the window, so the Run value has to say
        // it wants the tray on its own or every logon puts a window in somebody's face. The two
        // spellings are in two files and nothing else would notice them drifting apart.
        //
        // The line rather than the whole literal: Inno doubles its own quotes, so an exact match
        // asserts that escaping as much as it asserts the argument, and the argument is the claim.
        var runValue = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.Contains("ValueData:", StringComparison.Ordinal)
                && line.Contains("MyAppExeName", StringComparison.Ordinal));

        Assert.EndsWith(
            $"{Tray.Cli.CommandLine.TrayOnlyVerb}\"; \\",
            runValue.TrimEnd('\r'),
            StringComparison.Ordinal);

        // And it really is silence: the flag the script writes has to be the one that means it.
        var route = Tray.Cli.CommandLine.Of([Tray.Cli.CommandLine.TrayOnlyVerb]);
        Assert.Equal(Tray.Cli.Surface.Tray, route.Surface);
        Assert.False(route.OpenWindow);
    }

    [Fact]
    public void The_install_provisions_the_engine_and_only_where_the_preflight_cleared_it()
    {
        // DD119. Setup used to lay down one executable, print a preflight and stop, which left the
        // engine to a verb the wizard never named: `docker` was not a command, Start engine had no
        // distribution to boot, and the install looked finished.
        //
        // The order is the whole assertion. `RunPreflight` answers whether this machine can host an
        // engine, and the provision is behind that answer — unpacking one onto a machine that cannot
        // host it is the failure the preflight exists to prevent, and it costs a quarter of a
        // gigabyte to discover the hard way.
        var script = InstallerScript();

        Assert.Contains("if not RunPreflight then", script, StringComparison.Ordinal);
        Assert.Contains("WizardIsTaskSelected('engine')", script, StringComparison.Ordinal);
        Assert.Contains("'--provision'", script, StringComparison.Ordinal);

        var guard = script.IndexOf("if not RunPreflight then", StringComparison.Ordinal);
        var provision = script.IndexOf(
            "WizardIsTaskSelected('engine')", StringComparison.Ordinal);
        Assert.True(guard < provision, "the provision is no longer behind the preflight");
    }

    [Fact]
    public void The_engine_task_is_offered_ticked_so_a_default_install_has_an_engine()
    {
        // Unticking is the whole point of it being a task: a quarter of a gigabyte over somebody's
        // tethered connection is theirs to decline. Shipping it unticked is a different product —
        // one whose default install is the empty one DD119 was filed about.
        var task = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.StartsWith("Name: \"engine\";", StringComparison.Ordinal));

        Assert.DoesNotContain("unchecked", task, StringComparison.Ordinal);
    }

    [Fact]
    public void The_bar_the_installer_draws_has_one_notch_for_every_step_the_provisioner_runs()
    {
        // The installer counts step lines to move its progress bar and needs a total to divide by.
        // A step added to ProvisioningStep without this number moving leaves a successful install
        // with a bar that stops short of the end, which is what a failure looks like.
        var declared = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.TrimStart().StartsWith("ProvisioningSteps = ", StringComparison.Ordinal));

        Assert.Equal(
            Enum.GetValues<Core.Engine.ProvisioningStep>().Length,
            int.Parse(
                declared.Trim()["ProvisioningSteps = ".Length..].TrimEnd(';'),
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_verdict_the_installer_matches_is_the_one_the_verb_prints()
    {
        // A format string in C# and a Pos() call in Pascal, agreeing about six characters. Nothing
        // but this notices them drifting: the install would still succeed, and the bar would simply
        // never move — the least legible way for a download to look broken.
        var ok = Tray.Cli.EngineCommand.StepLine(
            new Core.Engine.StepResult(Core.Engine.ProvisioningStep.PlaceCli, true, "done"));
        var failed = Tray.Cli.EngineCommand.StepLine(
            new Core.Engine.StepResult(Core.Engine.ProvisioningStep.PlaceCli, false, "not done"));

        // Position 1 of the trimmed line, which is what the Pascal side matches on.
        Assert.StartsWith("[ok  ]", ok.Trim(), StringComparison.Ordinal);
        Assert.StartsWith("[FAIL]", failed.Trim(), StringComparison.Ordinal);

        var script = InstallerScript();
        Assert.Contains("Pos('[ok  ]', Line) = 1", script, StringComparison.Ordinal);
        Assert.Contains("Pos('[FAIL]', Line) = 1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_uninstall_takes_back_every_file_the_provision_placed_under_the_root()
    {
        // Inno removes what Inno installed, and the provision writes four things it did not: the
        // CLI, the plugin directory, and the two reports. Left behind, they are what keeps {app} on
        // disk after an uninstall that took everything else — and they are this product's own files,
        // not anybody's data, so they go without being asked about.
        //
        // The question that remains is about images and volumes, and it is the only one.
        var script = InstallerScript();
        var removal = script.IndexOf("RemovePathEntry;", StringComparison.Ordinal);
        var question = script.IndexOf("if not OwnedDataExists then", StringComparison.Ordinal);
        Assert.True(removal > 0 && question > removal);

        var unconditional = script[removal..question];
        foreach (var path in new[]
                 {
                     @"{app}\preflight.txt", @"{app}\provision.log",
                     @"{app}\bin", @"{app}\cli-plugins",
                 })
        {
            Assert.Contains($"'{path}'", unconditional, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_version_the_assembly_states_is_the_one_the_installer_would_read() =>
        // GetStringFileInfo(PRODUCT_VERSION) reads the informational version, which is what
        // BuildVersion prints. Two ways to ask, and they have to agree or the installed version and
        // the About box disagree about the same build.
        Assert.Equal(
            Shipped.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            BuildVersion.Current);
}
