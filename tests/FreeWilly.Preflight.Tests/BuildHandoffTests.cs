using FreeWilly.Core.Builds;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The ref one launch hands to the window another launch is already showing (DD126).
/// </summary>
/// <remarks>
/// A scratch file per test, never the real one: this writes and deletes, and the machine running the
/// suite must not be what it experiments on — the same rule the autostart tests follow.
/// </remarks>
public class BuildHandoffTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"freewilly-handoff-{Guid.NewGuid():N}.txt");

    private const string Ref = "default/default/i93abaotri2m3vdda5unxeimu";

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // The test that mattered has already run.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void What_was_left_is_what_is_taken()
    {
        var handoff = new BuildHandoff(_path);

        Assert.True(handoff.Leave(Ref));
        Assert.Equal(Ref, handoff.Take());
    }

    [Fact]
    public void Taking_it_removes_it()
    {
        // The whole reason it is "take" and not "read": a ref left behind would make the next
        // ordinary launch open a build nobody asked for.
        var handoff = new BuildHandoff(_path);
        handoff.Leave(Ref);

        Assert.Equal(Ref, handoff.Take());
        Assert.Null(handoff.Take());
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void Nothing_waiting_is_not_a_failure()
    {
        // The ordinary case on every launch that did not come from a link.
        Assert.Null(new BuildHandoff(_path).Take());
    }

    [Fact]
    public void The_second_link_wins()
    {
        // Two clicked in a row should open the second, which is what the user just did.
        var handoff = new BuildHandoff(_path);
        handoff.Leave("default/default/aaa");
        handoff.Leave("default/default/bbb");

        Assert.Equal("default/default/bbb", handoff.Take());
    }

    [Fact]
    public void What_comes_off_disk_is_validated_on_the_way_out_too()
    {
        // The file sits between two processes, so what is read here is not necessarily what this
        // wrote — and the ref goes on to become a subprocess argument.
        File.WriteAllText(_path, @"..\..\Windows\System32\calc.exe");

        Assert.Null(new BuildHandoff(_path).Take());
    }

    [Fact]
    public void A_link_written_whole_is_still_read_as_a_ref()
    {
        // Leave is called with a ref today. Reading through the same parser means a future caller
        // that leaves the URL instead is not a silent failure.
        File.WriteAllText(_path, $"docker-desktop://dashboard/build/{Ref}");

        Assert.Equal(Ref, new BuildHandoff(_path).Take());
    }

    [Fact]
    public void A_directory_that_does_not_exist_yet_is_created_rather_than_refused()
    {
        // A first run can reach this before anything else has made the root.
        var nested = Path.Combine(
            Path.GetTempPath(), $"freewilly-handoff-{Guid.NewGuid():N}", "open-build.txt");
        try
        {
            Assert.True(new BuildHandoff(nested).Leave(Ref));
            Assert.Equal(Ref, new BuildHandoff(nested).Take());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(nested)!, recursive: true);
        }
    }
}
