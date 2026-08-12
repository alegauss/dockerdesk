using System.Formats.Tar;
using System.IO.Compression;

namespace DockerDesk.Core.Engine;

/// <summary>
/// What is inside the engine tarball, read before anything is unpacked into a distribution.
/// </summary>
/// <remarks>
/// Upstream's layout is not a promise. Without this check a renamed or dropped binary surfaces as a
/// shell error from inside the distribution — after the import, in a place with no terminal, saying
/// something about tar rather than something about the engine. Read locally it is one named step.
/// </remarks>
public static class EngineArchive
{
    /// <summary>
    /// The binaries the engine cannot run without. Not the whole tarball: <c>ctr</c> and
    /// <c>docker-init</c> ship with it and nothing here breaks if upstream stops shipping them.
    /// </summary>
    public static IReadOnlyList<string> RequiredBinaries { get; } =
    [
        "dockerd",
        "containerd",
        "containerd-shim-runc-v2",
        "runc",
        "docker-proxy",
    ];

    /// <summary>What the tarball holds.</summary>
    /// <param name="TopDirectory">
    /// The single directory every entry sits under, which is what <c>--strip-components=1</c>
    /// removes. Null when the entries do not share one, which is itself the defect.
    /// </param>
    /// <param name="Binaries">The leaf names of the regular files, in archive order.</param>
    public sealed record Contents(string? TopDirectory, IReadOnlyList<string> Binaries);

    /// <summary>Read the member list of a gzip-compressed tar.</summary>
    /// <param name="path">The <c>.tgz</c>.</param>
    /// <returns>What is in it.</returns>
    public static Contents Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directories = new HashSet<string>(StringComparer.Ordinal);
        var binaries = new List<string>();

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        while (tar.GetNextEntry() is { } entry)
        {
            var name = entry.Name.Replace('\\', '/').TrimStart('/');
            var slash = name.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0)
            {
                // An entry at the root has no directory to strip, so the archive is not the shape
                // `--strip-components=1` is written for. Recorded as "no shared top".
                directories.Add("");
                continue;
            }

            directories.Add(name[..slash]);
            if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
            {
                binaries.Add(name[(slash + 1)..]);
            }
        }

        var top = directories.Count == 1 ? directories.Single() : null;
        return new Contents(string.IsNullOrEmpty(top) ? null : top, binaries);
    }

    /// <summary>
    /// The binaries <see cref="RequiredBinaries"/> names that <paramref name="contents"/> does not
    /// have. Empty means the archive is usable.
    /// </summary>
    /// <param name="contents">What was read.</param>
    /// <returns>What is missing, in the order it is required.</returns>
    public static IReadOnlyList<string> Missing(Contents contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        var present = new HashSet<string>(contents.Binaries, StringComparer.OrdinalIgnoreCase);
        return [.. RequiredBinaries.Where(required => !present.Contains(required))];
    }
}
