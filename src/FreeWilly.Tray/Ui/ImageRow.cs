using System.Globalization;
using FreeWilly.Core.Api;

namespace FreeWilly.Tray.Ui;

/// <summary>Bytes, as a person reads them.</summary>
public static class Bytes
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Render a size the way every other Docker tool renders it.
    /// </summary>
    /// <remarks>
    /// Decimal units, because that is what <c>docker images</c> prints and a list that disagrees
    /// with the CLI about how big the same image is invites the user to distrust both.
    /// </remarks>
    /// <param name="bytes">The size.</param>
    /// <returns>Something like <c>1.2 GB</c>.</returns>
    public static string Human(long bytes)
    {
        if (bytes < 1000)
        {
            return $"{bytes} B";
        }

        double size = bytes;
        var unit = 0;
        while (size >= 1000 && unit < Units.Length - 1)
        {
            size /= 1000;
            unit++;
        }

        // One decimal below ten, none above: "9.4 GB" and "48 MB" are both the precision somebody
        // deciding what to delete actually uses.
        return size < 10
            ? string.Create(CultureInfo.InvariantCulture, $"{size:0.0} {Units[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{size:0} {Units[unit]}");
    }
}

/// <summary>An image, as the window shows it.</summary>
/// <param name="Repository">The repository, or <c>&lt;none&gt;</c> when it has no tag.</param>
/// <param name="Tag">The tag, or <c>&lt;none&gt;</c>.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="UsedBy">The containers holding it, by name.</param>
/// <param name="IsDangling">Whether it is untagged.</param>
/// <param name="Id">The full id, which is what a removal is addressed to.</param>
public sealed record ImageRow(
    string Repository,
    string Tag,
    long Size,
    IReadOnlyList<string> UsedBy,
    bool IsDangling,
    string Id)
{
    /// <summary>What this row is waiting for, or nothing. Same contract as a container row.</summary>
    public string? Pending { get; init; }

    /// <summary>The engine's own sentence about why the last removal failed, or nothing.</summary>
    public string? Failure { get; init; }

    /// <summary>Whether a removal is in flight.</summary>
    public bool IsPending => Pending is not null;

    /// <summary>Whether the row offers its button.</summary>
    public bool IsIdle => Pending is null;

    /// <summary>Whether the last removal failed.</summary>
    public bool HasFailure => Failure is not null;

    /// <summary>What the size column reads.</summary>
    public string SizeText => Bytes.Human(Size);

    /// <summary>Whether a container references it.</summary>
    public bool IsInUse => UsedBy.Count > 0;

    /// <summary>
    /// The two facts the decision is made on, in one column.
    /// </summary>
    /// <remarks>
    /// "Which of these can I delete" is answered by exactly this: whether anything holds it, and
    /// whether it is an untagged intermediate nothing will ever ask for by name. The CLI's answer
    /// is three commands and a join done in the reader's head.
    /// </remarks>
    public string Status => this switch
    {
        { IsInUse: true } => string.Join(", ", UsedBy),
        { IsDangling: true } => "dangling",
        _ => "unused",
    };

    /// <summary>What the row is called, for a message about it.</summary>
    public string Name => IsDangling ? Id[..Math.Min(Id.Length, 19)] : $"{Repository}:{Tag}";


    /// <summary>The headings this list sorts on.</summary>
    public static class Columns
    {
        /// <summary>The repository half of the tag.</summary>
        public const string Repository = "REPOSITORY";

        /// <summary>The tag half.</summary>
        public const string Tag = "TAG";

        /// <summary>What it costs on disk.</summary>
        public const string Size = "SIZE";

        /// <summary>Which containers hold it.</summary>
        public const string UsedBy = "USED BY";
    }

    /// <summary>The order an image list opens in: biggest first, which DD11 chose and is right.</summary>
    public const string DefaultColumn = Columns.Size;

    /// <summary>Shape a list of rows: narrowed, then ordered.</summary>
    /// <param name="rows">What the join produced.</param>
    /// <param name="shape">The sort and filter the page is holding.</param>
    /// <returns>The rows to draw.</returns>
    public static IReadOnlyList<ImageRow> Shaped(IEnumerable<ImageRow> rows, ListShape shape)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(shape);

        var kept = rows.Where(row => shape.Keeps(
            row.Repository, row.Tag, string.Join(" ", row.UsedBy)));

        IOrderedEnumerable<ImageRow> ordered = shape.Column switch
        {
            Columns.Tag => By(kept, r => r.Tag, shape.Descending, StringComparer.OrdinalIgnoreCase),
            Columns.UsedBy => By(kept, r => r.UsedBy.Count, shape.Descending),
            Columns.Repository => By(kept, r => r.Repository, shape.Descending, StringComparer.OrdinalIgnoreCase),
            _ => By(kept, r => r.Size, shape.Descending),
        };

        return [.. ordered
            .ThenBy(row => row.Repository, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Tag, StringComparer.OrdinalIgnoreCase)];
    }

    private static IOrderedEnumerable<ImageRow> By<TKey>(
        IEnumerable<ImageRow> rows,
        Func<ImageRow, TKey> key,
        bool descending,
        IComparer<TKey>? comparer = null) =>
        descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);

    /// <summary>
    /// Project the daemon's two lists into one, joined on image id and sorted by size.
    /// </summary>
    /// <remarks>
    /// The join is the whole point. The daemon will say how big an image is and it will say what a
    /// container was created from, and nothing in the API puts those together — so a user deciding
    /// what to reclaim does it by eye across two outputs, which is the work this replaces.
    ///
    /// Sorted descending because the decision is about disk, and the row worth looking at first is
    /// the biggest one.
    /// </remarks>
    /// <param name="images">What the image endpoint returned.</param>
    /// <param name="containers">What the container endpoint returned, stopped ones included.</param>
    /// <returns>The rows, largest first.</returns>
    public static IReadOnlyList<ImageRow> From(
        IEnumerable<ImageSummary> images, IEnumerable<ContainerSummary> containers)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(containers);

        var holders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var container in containers)
        {
            if (container.ImageId.Length == 0)
            {
                continue;
            }

            if (!holders.TryGetValue(container.ImageId, out var names))
            {
                holders[container.ImageId] = names = [];
            }

            names.Add(container.DisplayName);
        }

        return
        [
            .. images
                .Select(image =>
                {
                    var tag = FirstTag(image);
                    var split = tag?.LastIndexOf(':') ?? -1;
                    return new ImageRow(
                        split > 0 ? tag![..split] : "<none>",
                        split > 0 ? tag![(split + 1)..] : "<none>",
                        image.Size,
                        holders.TryGetValue(image.Id, out var names) ? names : [],
                        tag is null,
                        image.Id);
                })
                .OrderByDescending(row => row.Size)
                .ThenBy(row => row.Repository, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The first real tag, or nothing when the image is dangling.
    /// </summary>
    /// <remarks>
    /// An untagged image comes back either with no tags at all or with the literal
    /// <c>&lt;none&gt;:&lt;none&gt;</c>. Both are the same image and only one of them looks like a
    /// tag.
    /// </remarks>
    private static string? FirstTag(ImageSummary image) =>
        image.RepoTags?.FirstOrDefault(tag =>
            tag.Length > 0 && !tag.StartsWith("<none>", StringComparison.Ordinal));
}

/// <summary>What the images tab says above the list, and what prune offers.</summary>
/// <param name="Total">Every image, added up.</param>
/// <param name="Reclaimable">What a dangling-only prune would return.</param>
/// <param name="DanglingCount">How many rows that is.</param>
public sealed record ImageTotals(long Total, long Reclaimable, int DanglingCount)
{
    /// <summary>Add up a list.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The totals.</returns>
    public static ImageTotals For(IReadOnlyList<ImageRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // Dangling and in use at once is real — a container built from an image whose tag later
        // moved — and prune leaves those alone, so counting them as reclaimable would promise
        // space that is not coming back.
        var loose = rows.Where(row => row is { IsDangling: true, IsInUse: false }).ToList();
        return new ImageTotals(
            rows.Sum(row => row.Size), loose.Sum(row => row.Size), loose.Count);
    }

    /// <summary>The line above the list.</summary>
    public string Summary =>
        $"{Bytes.Human(Total)} in images"
        + (DanglingCount > 0
            ? $" · {Bytes.Human(Reclaimable)} reclaimable from "
              + (DanglingCount == 1 ? "1 dangling image" : $"{DanglingCount} dangling images")
            : " · nothing dangling");

    /// <summary>Whether the prune button does anything.</summary>
    public bool CanPrune => DanglingCount > 0;

    /// <summary>
    /// What prune asks before it runs, with the number named.
    /// </summary>
    /// <remarks>
    /// The total is stated before the click rather than after, because "are you sure" about an
    /// unknown quantity is a question nobody can answer. Only dangling images go: <c>prune -a</c>
    /// deletes every image no *running* container uses, which on a developer's machine is most of
    /// them, and that half of the sentence is not on the command's own warning.
    /// </remarks>
    public string PruneQuestion =>
        $"Delete {(DanglingCount == 1 ? "1 dangling image" : $"{DanglingCount} dangling images")} "
        + $"and reclaim about {Bytes.Human(Reclaimable)}?\n\n"
        + "Only untagged images that no container references are removed. Tagged images stay.";

    /// <summary>What the window says once the daemon has answered.</summary>
    /// <param name="pruned">What came back.</param>
    /// <returns>One sentence.</returns>
    public static string PruneOutcome(ImagesPruned pruned)
    {
        ArgumentNullException.ThrowIfNull(pruned);

        var count = pruned.ImagesDeleted?.Count(entry => entry.Deleted is not null) ?? 0;
        return count == 0 && pruned.SpaceReclaimed == 0
            ? "Nothing was dangling by the time the engine looked."
            : $"Removed {(count == 1 ? "1 image" : $"{count} images")}, "
              + $"reclaimed {Bytes.Human(pruned.SpaceReclaimed)}.";
    }
}
