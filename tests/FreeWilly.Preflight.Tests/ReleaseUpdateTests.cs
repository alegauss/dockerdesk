using System.Security.Cryptography;
using System.Text;
using FreeWilly.Core.Releases;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Reading the digest a release published, and refusing an installer that does not have it (DD154).
/// </summary>
public sealed class ReleaseUpdateTests
{
    /// <summary>A directory of this test's own, removed when it is done.</summary>
    private sealed class Scratch : IDisposable
    {
        internal string Directory { get; } =
            Path.Combine(Path.GetTempPath(), $"freewilly-update-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A temp directory that outlives the run costs nothing worth failing a suite over.
            }
        }
    }

    private const string InstallerName = "FreeWilly-Setup-2.0.0.exe";

    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("pretend this is an installer");

    private static string Digest =>
        Convert.ToHexStringLower(SHA256.HashData(Bytes));

    private static AvailableRelease Release() => new(
        new Version(2, 0, 0),
        "v2.0.0",
        InstallerName,
        $"https://example.invalid/{InstallerName}",
        $"https://example.invalid/{ReleaseCheck.SumsAssetName}");

    private static FakeFetcher Serving(string sums, byte[]? installer) => new(url =>
        url.EndsWith(ReleaseCheck.SumsAssetName, StringComparison.Ordinal)
            ? Encoding.ASCII.GetBytes(sums)
            : installer);

    [Fact]
    public async Task An_installer_matching_the_published_digest_is_left_on_disk()
    {
        using var scratch = new Scratch();
        var fetcher = Serving($"{Digest}  {InstallerName}\n", Bytes);

        var fetched = await new ReleaseUpdate(fetcher, scratch.Directory).FetchAsync(Release());

        Assert.True(fetched.Verified);
        Assert.Equal(Path.Combine(scratch.Directory, InstallerName), fetched.Path);
        Assert.True(File.Exists(fetched.Path));

        // The sums file first, because without it there is nothing to check the installer against
        // and no reason to spend the larger download.
        Assert.EndsWith(ReleaseCheck.SumsAssetName, fetcher.Requested[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installer_that_arrived_wrong_is_deleted_rather_than_run()
    {
        // The failure this whole file exists for. A digest that does not match is not evidence of
        // anything except that the file must not be executed, and leaving it would let a retry find
        // it and pass.
        using var scratch = new Scratch();
        var wrong = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("something else")));

        var fetched = await new ReleaseUpdate(Serving($"{wrong}  {InstallerName}\n", Bytes), scratch.Directory)
            .FetchAsync(Release());

        Assert.False(fetched.Verified);
        Assert.NotNull(fetched.Failure);
        Assert.False(File.Exists(Path.Combine(scratch.Directory, InstallerName)));
    }

    [Fact]
    public async Task A_sums_file_that_does_not_name_the_installer_stops_before_the_download()
    {
        // No digest is the same answer as no installer, and the larger download is not even started:
        // there would be nothing to compare it against.
        using var scratch = new Scratch();
        var fetcher = Serving($"{Digest}  something-else.exe\n", Bytes);

        var fetched = await new ReleaseUpdate(fetcher, scratch.Directory).FetchAsync(Release());

        Assert.False(fetched.Verified);
        Assert.Contains(InstallerName, fetched.Failure, StringComparison.Ordinal);
        Assert.Single(fetcher.Requested);
    }

    [Fact]
    public async Task A_sums_file_that_could_not_be_downloaded_is_said_rather_than_thrown()
    {
        using var scratch = new Scratch();

        var fetched = await new ReleaseUpdate(new FakeFetcher(_ => null), scratch.Directory)
            .FetchAsync(Release());

        Assert.False(fetched.Verified);
        Assert.Contains(ReleaseCheck.SumsAssetName, fetched.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installer_that_could_not_be_downloaded_is_said_rather_than_thrown()
    {
        using var scratch = new Scratch();

        var fetched = await new ReleaseUpdate(
                Serving($"{Digest}  {InstallerName}\n", null), scratch.Directory)
            .FetchAsync(Release());

        Assert.False(fetched.Verified);
        Assert.NotNull(fetched.Failure);
    }

    [Fact]
    public void The_installer_is_run_silently_and_asked_to_relaunch()
    {
        // The switch installer.iss checks, and the reason its [Run] entry can stay skipifsilent: an
        // unattended install still puts no icon in anybody's session, and a self-update still comes
        // back. PackagingTests holds the two files to each other.
        Assert.Contains("/SILENT", ReleaseUpdate.SilentArguments, StringComparison.Ordinal);
        Assert.Contains("/RELAUNCH=yes", ReleaseUpdate.SilentArguments, StringComparison.Ordinal);
    }

    [Fact]
    public void A_downloaded_installer_is_litter_and_goes_where_litter_goes()
    {
        // Deliberately not EnginePaths.Downloads, which exists so a repeated provision does not
        // re-fetch a quarter of a gigabyte. An installer is used once.
        Assert.StartsWith(Path.GetTempPath(), ReleaseUpdate.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("d  name.exe", null)]
    [InlineData("", null)]
    [InlineData("nothing here", null)]
    public void A_sums_line_that_is_not_a_sha256_names_no_digest(string text, string? expected) =>
        Assert.Equal(expected, ReleaseSums.DigestFor(text, "name.exe"));

    [Fact]
    public void The_shape_sha256sum_writes_is_the_shape_this_reads()
    {
        // Two spaces is what release.yml writes; one space and a binary-mode star are what other
        // tools write, and refusing a correct digest over whitespace would be a failure with no
        // remedy anybody could apply.
        var digest = new string('a', 64);

        Assert.Equal(digest, ReleaseSums.DigestFor($"{digest}  file.exe", "file.exe"));
        Assert.Equal(digest, ReleaseSums.DigestFor($"{digest} file.exe", "file.exe"));
        Assert.Equal(digest, ReleaseSums.DigestFor($"{digest} *file.exe", "file.exe"));
        Assert.Equal(digest, ReleaseSums.DigestFor($"other.exe\n{digest}  file.exe\n", "file.exe"));
    }

    [Fact]
    public void Two_lines_disagreeing_about_one_file_name_no_digest_at_all()
    {
        // A sums file nobody can act on. Identical duplicate lines are harmless; disagreeing ones
        // are the case that must not resolve to whichever was read last.
        var one = new string('a', 64);
        var two = new string('b', 64);

        Assert.Equal(one, ReleaseSums.DigestFor($"{one}  f.exe\n{one}  f.exe", "f.exe"));
        Assert.Null(ReleaseSums.DigestFor($"{one}  f.exe\n{two}  f.exe", "f.exe"));
    }
}
