using System.Text.Json;
using DockerDesk.Core.Agent;
using DockerDesk.Core.Api;
using DockerDesk.Tray.Cli;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// Shipping the surface includes shipping how it is found (DD32).
/// </summary>
/// <remarks>
/// Two properties are worth a test rather than a review. The skill must name exactly the verbs that
/// exist, or the file loaded every session is the one that drifts unnoticed. And the install must
/// propose rather than write: an allowlist is a permission grant, and a tool that edits one without
/// asking has broken the rule that nothing here surprises you.
/// </remarks>
public sealed class AgentDiscoveryTests
{
    private static string RepositoryFile(string name)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            here = here.Parent;
        }

        throw new InvalidOperationException(name + " was not found");
    }

    private static string Skill() => File.ReadAllText(RepositoryFile(@"build\agent\SKILL.md"));

    /// <summary>The lines inside the skill's verb block.</summary>
    private static IReadOnlyList<string> ListedVerbs()
    {
        var text = Skill().ReplaceLineEndings("\n");

        // The block after the generated-from comment, so a fenced example elsewhere in the file is
        // not mistaken for the list.
        var marker = text.IndexOf("A test holds this list equal", StringComparison.Ordinal);
        Assert.True(marker > 0, "the skill no longer carries the marker this test finds the list by");

        var open = text.IndexOf("```", marker, StringComparison.Ordinal);
        var close = text.IndexOf("```", open + 3, StringComparison.Ordinal);
        Assert.True(open > 0 && close > open, "the verb block is not fenced");

        return [.. text[(open + 3)..close]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    // ---- one description, not two --------------------------------------------------------------

    [Fact]
    public void The_skill_names_exactly_the_verbs_the_registry_has()
    {
        // A verb that lands without appearing here fails the build, and one that appears here after
        // being retired fails it too. That is the whole reason the list is machine-checked: the file
        // is loaded every session, so nobody is reading it against the code.
        Assert.Equal(
            AgentSurface.All.Select(verb => verb.ToString()).ToArray(),
            ListedVerbs());
    }

    [Fact]
    public void The_skill_names_verbs_and_explains_none_of_them()
    {
        var skill = Skill();
        foreach (var verb in AgentSurface.All)
        {
            // Every sentence explaining what a verb does lives in --help, which is one copy. Two
            // descriptions of one surface drift.
            Assert.DoesNotContain(verb.Summary, skill, StringComparison.Ordinal);
        }

        Assert.Contains("dockerdesk --help", skill, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skill_says_the_one_rule_that_makes_the_surface_worth_finding()
    {
        var skill = Skill();
        Assert.Contains("before `docker`", skill, StringComparison.Ordinal);
        Assert.Contains(AgentBrief.AllowEntry, skill, StringComparison.Ordinal);
    }

    // ---- the install proposes and never writes -------------------------------------------------

    [Fact]
    public void The_snippet_grants_every_read_and_no_write()
    {
        using var snippet = JsonDocument.Parse(
            File.ReadAllBytes(RepositoryFile(@"build\agent\settings-snippet.json")));

        var allow = snippet.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

        Assert.Equal([AgentBrief.AllowEntry], allow);

        // `do` is deliberately absent: granting it here would undo the reason the split exists.
        Assert.DoesNotContain(allow, entry => entry.Contains("do ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_install_never_touches_an_agent_configuration()
    {
        // The property, asserted against the installer script itself rather than trusted to a
        // comment: no directive in it may name the directory where a user's agent settings live.
        // Comments are stripped first - the reasoning for this rule is written in one, and a guard
        // that its own explanation fails is a guard nobody keeps.
        var installer = string.Join(
            "\n",
            File.ReadAllLines(RepositoryFile(@"build\installer.iss"))
                .Where(line => !line.TrimStart().StartsWith(';')));

        Assert.DoesNotContain(".claude", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings.json", installer, StringComparison.OrdinalIgnoreCase);

        // And it does ship them, or there would be nothing to propose.
        Assert.Contains(@"agent\SKILL.md", installer, StringComparison.Ordinal);
        Assert.Contains(@"agent\settings-snippet.json", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void The_after_install_page_names_both_files_and_the_line_to_add()
    {
        var page = File.ReadAllText(RepositoryFile(@"build\after-install.txt"));

        Assert.Contains(@"agent\SKILL.md", page, StringComparison.Ordinal);
        Assert.Contains(@"agent\settings-snippet.json", page, StringComparison.Ordinal);
        Assert.Contains(AgentBrief.AllowEntry, page, StringComparison.Ordinal);

        // Inno reads this file as ANSI and renders it in a fixed-width page. A curly quote or a dash
        // that is not a hyphen arrives as mojibake in front of somebody finishing an install.
        Assert.All(page, character => Assert.True(
            character < 128,
            $"after-install.txt carries U+{(int)character:X4}, which Inno will not render as written"));
    }

    // ---- the brief -----------------------------------------------------------------------------

    private static ContextFacts Facts() => new(
        EngineState: "running",
        Distribution: "dockerdesk",
        ApiVersion: "1.43",
        ContextName: "default",
        ContextReachesEngine: true,
        Containers: [],
        Diagnoses: new Dictionary<string, ContainerInspect>(StringComparer.Ordinal),
        Images: [],
        VolumeCount: 0);

    [Fact]
    public void The_brief_names_every_verb_and_explains_none()
    {
        var brief = AgentBrief.Render(Facts(), [.. AgentSurface.All.Select(v => v.ToString())]);

        foreach (var verb in AgentSurface.All)
        {
            Assert.Contains(verb.ToString(), brief, StringComparison.Ordinal);
            Assert.DoesNotContain(verb.Summary, brief, StringComparison.Ordinal);
        }

        Assert.Contains(AgentBrief.AllowEntry, brief, StringComparison.Ordinal);
    }

    // ---- writing it, and refusing to eat what somebody wrote -----------------------------------

    private static string Path(string endpoint) => $"/{DockerApi.ApiVersion}/{endpoint}";

    private static FakeDockerDaemon Daemon() => new FakeDockerDaemon()
        .Fails(Path("_ping"), "200 OK", "OK")
        .Json(Path("version"), """{"Version":"29.7.2","ApiVersion":"1.55","MinAPIVersion":"1.24","Os":"linux","Arch":"amd64"}""")
        .Json(Path("containers/json?all=1"), "[]")
        .Json(Path("images/json?all=0"), "[]")
        .Json(Path("volumes"), """{"Volumes":[]}""");

    private static int Context(FakeDockerDaemon daemon, string[] arguments, TextWriter output)
    {
        using var api = new DockerApi(daemon.PipeName);
        return AgentSurface.Read(AgentSurface.Find(["read", "context"])!, api, arguments, output);
    }

    [Fact]
    public async Task With_no_path_the_brief_goes_to_stdout_like_every_other_verb()
    {
        await using var daemon = Daemon();
        var output = new StringWriter();

        var code = Context(daemon, ["--as", "brief"], output);

        Assert.Equal(0, code);
        Assert.Contains(AgentBrief.AllowEntry, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_writes_where_it_was_told_and_refuses_to_replace_what_is_there()
    {
        var scratch = Directory.CreateTempSubdirectory("dockerdesk-brief");
        try
        {
            var target = System.IO.Path.Combine(scratch.FullName, "nested", "brief.md");

            await using (var daemon = Daemon())
            {
                Assert.Equal(0, Context(daemon, ["--as", "brief", "--out", target], new StringWriter()));
            }

            Assert.Contains(AgentBrief.AllowEntry, File.ReadAllText(target), StringComparison.Ordinal);

            // Somebody edited it. A generated file landing on top of that is the trade this refuses
            // to make on their behalf.
            File.WriteAllText(target, "mine");

            await using (var daemon = Daemon())
            {
                Assert.Equal(2, Context(daemon, ["--as", "brief", "--out", target], new StringWriter()));
            }

            Assert.Equal("mine", File.ReadAllText(target));

            await using (var daemon = Daemon())
            {
                Assert.Equal(
                    0,
                    Context(daemon, ["--as", "brief", "--out", target, "--force"], new StringWriter()));
            }

            Assert.NotEqual("mine", File.ReadAllText(target));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_stopped_engine_still_writes_the_brief_and_says_the_machine_part_is_empty()
    {
        // Found by running it rather than by writing it: with the engine down, `--as brief --out`
        // used to print the context pack and write no file at all, so the caller got neither the
        // brief they asked for nor a reason. Most of a brief is how this surface is reached, which
        // is true whether the engine is up or not, and setting a project up before starting the
        // engine is the ordinary case.
        var scratch = Directory.CreateTempSubdirectory("dockerdesk-brief-down");
        try
        {
            var target = System.IO.Path.Combine(scratch.FullName, "brief.md");
            await using var daemon = new FakeDockerDaemon()
                .Fails(Path("_ping"), "500 Internal Server Error", "no");

            var code = Context(daemon, ["--as", "brief", "--out", target], new StringWriter());

            // Non-zero, because the machine section came from a stopped engine.
            Assert.Equal(3, code);
            Assert.Contains(AgentBrief.AllowEntry, File.ReadAllText(target), StringComparison.Ordinal);
            Assert.Contains("stopped", File.ReadAllText(target), StringComparison.Ordinal);

            // And a refusal about the arguments keeps its own code even down here: "engine not
            // ready" for a file that already exists would send a script down the wrong branch over
            // a problem that has nothing to do with the engine.
            await using var again = new FakeDockerDaemon()
                .Fails(Path("_ping"), "500 Internal Server Error", "no");
            Assert.Equal(2, Context(again, ["--as", "brief", "--out", target], new StringWriter()));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task An_out_with_nothing_to_write_is_refused_rather_than_ignored()
    {
        await using var daemon = Daemon();

        // Quietly accepting it would teach an argument that does nothing, which costs a session the
        // call that discovers the file was never written.
        Assert.Equal(2, Context(daemon, ["--out", "brief.md"], new StringWriter()));
        Assert.Empty(daemon.Requested);
    }

    [Fact]
    public void The_brief_is_the_same_bytes_on_an_unchanged_machine()
    {
        // No timestamp, on purpose: it lives in a repository, and a file whose only diff is the hour
        // it was generated teaches everybody to stop reading its diffs.
        var verbs = AgentSurface.All.Select(v => v.ToString()).ToArray();
        Assert.Equal(AgentBrief.Render(Facts(), verbs), AgentBrief.Render(Facts(), verbs));
    }
}
