using FreeWilly.Core.Agent;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Asking the distribution about a bind source (DD101).
/// </summary>
/// <remarks>
/// The invocation rather than its effect, for the reason <see cref="FakeWsl"/> exists at all: no test
/// machine has this engine's distribution registered, and one that did would be answering about its
/// own filesystem. What is worth holding is the shape of the command and how its output is read,
/// because both are what decide whether a user's code is called missing.
/// </remarks>
public sealed class BindSourceTests
{
    private static BindSources Reader(FakeWsl wsl) => new(wsl, "freewilly");

    [Theory]
    [InlineData("missing", BindSource.Missing)]
    [InlineData("empty", BindSource.Empty)]
    [InlineData("holds", BindSource.Holds)]
    [InlineData("holds\n", BindSource.Holds)]
    [InlineData("  empty  ", BindSource.Empty)]
    public void The_three_words_the_script_prints_are_the_three_answers(string output, BindSource answer)
    {
        var wsl = new FakeWsl();
        _ = wsl.Answer(0, output);

        Assert.Equal(answer, Reader(wsl).Look("/home/you/project"));
    }

    [Theory]
    [InlineData("a feature is missing from this build")]
    [InlineData("")]
    [InlineData("wsl: something else entirely")]
    public void Anything_the_script_did_not_print_is_not_an_answer(string output)
    {
        // Matched whole rather than searched for. wsl.exe is entitled to print a warning of its own
        // alongside, and a substring search for "missing" in a line about a missing feature would
        // report the user's code as absent on the strength of Windows clearing its throat.
        var wsl = new FakeWsl();
        _ = wsl.Answer(0, output);

        Assert.Equal(BindSource.Unasked, Reader(wsl).Look("/home/you/project"));
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(null, "")]
    public void A_run_that_did_not_succeed_asked_nothing(int? exitCode, string output)
    {
        // A stopped distribution and a wsl.exe that is not there both land here, and neither is
        // evidence about the path. Unasked keeps the row saying "unchecked", which is what it said
        // before this read existed.
        var wsl = new FakeWsl();
        _ = wsl.Answer(exitCode, output, exitCode is null ? "wsl.exe could not be started" : null);

        Assert.Equal(BindSource.Unasked, Reader(wsl).Look("/home/you/project"));
    }

    [Fact]
    public void An_empty_source_is_not_worth_a_subprocess()
    {
        var wsl = new FakeWsl();

        Assert.Equal(BindSource.Unasked, Reader(wsl).Look("  "));
        Assert.Empty(wsl.Invocations);
    }

    [Fact]
    public void The_path_arrives_as_an_argument_and_never_as_text_in_the_script()
    {
        // The rule worth a test: the source comes from a container's own configuration, so a path
        // holding a quote, a space or a $ must not be able to change what runs. `--exec` hands the
        // arguments straight to /bin/sh rather than through the distribution's login shell, and the
        // script reads the source as $1.
        var wsl = new FakeWsl();
        const string awkward = "/home/you/my project; rm -rf $HOME";
        _ = Reader(wsl).Look(awkward);

        var invocation = Assert.Single(wsl.Invocations);
        Assert.Equal(["-d", "freewilly", "--exec", "/bin/sh", "-c", BindSources.Script, "sh", awkward],
            invocation);
        Assert.DoesNotContain(awkward, BindSources.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Emptiness_is_decided_with_builtins_and_counts_dotfiles()
    {
        // The engine's rootfs is not a general-purpose Linux install, so nothing here may depend on
        // what is in its PATH — and a directory holding only `.git` is not an empty one, which is
        // why the glob has the three patterns rather than the obvious one.
        Assert.DoesNotContain("ls ", BindSources.Script, StringComparison.Ordinal);
        Assert.Contains("\"$1\"/.[!.]*", BindSources.Script, StringComparison.Ordinal);
        Assert.Contains("\"$1\"/..?*", BindSources.Script, StringComparison.Ordinal);
    }
}
