namespace DockerDesk.Tray.Ui;

/// <summary>
/// How a list is currently ordered and narrowed, kept by the page across refreshes (DD37).
/// </summary>
/// <remarks>
/// Every heading in this window used to be a dead <c>TextBlock</c>, and the order a list arrived in was
/// the order it was read in — creation order for containers, which answers nothing. There was no filter
/// anywhere, and a machine with thirty images and a dozen containers was reached with the scrollbar.
///
/// <para><b>The part that is easy to get wrong is the refresh.</b> This window redraws on every engine
/// event, and a sort or a filter held only by the controls would be thrown away each time — the list
/// would snap back to its default while somebody was reading it. So the shape is a value the page owns,
/// and every refresh re-applies it to the rows that just arrived.</para>
///
/// <para>Filtering is over the rows in hand and never a second call to the daemon: the fields being
/// matched are already on the row, and asking the engine to search would be a round trip to answer a
/// question the window can already answer.</para>
/// </remarks>
/// <param name="Column">Which heading is sorted on.</param>
/// <param name="Descending">Which way.</param>
/// <param name="Filter">What was typed, or empty.</param>
public sealed record ListShape(string Column, bool Descending, string Filter = "")
{
    /// <summary>Whether anything was typed into the filter box.</summary>
    public bool IsFiltered => Filter.Trim().Length > 0;

    /// <summary>What a sorted heading shows beside its caption.</summary>
    /// <param name="column">The heading asking.</param>
    /// <returns>The glyph, or an empty string for every other heading.</returns>
    /// <remarks>
    /// Only the sorted one carries it. A glyph on every heading would be six affordances competing to
    /// say which one is in force.
    /// </remarks>
    public string GlyphFor(string column) =>
        string.Equals(Column, column, StringComparison.Ordinal)
            ? Descending ? " ↓" : " ↑"
            : "";

    /// <summary>
    /// The shape after a heading is clicked.
    /// </summary>
    /// <param name="column">The heading.</param>
    /// <param name="descendsFirst">
    /// Whether this column reads best biggest-first. A size does; a name does not.
    /// </param>
    /// <returns>The new shape.</returns>
    /// <remarks>
    /// Clicking the column already sorted flips it; clicking a different one starts at that column's
    /// own natural direction rather than at whatever the last one happened to be. Sorting by SIZE and
    /// getting the smallest image first is the sort nobody wanted.
    /// </remarks>
    public ListShape Toggled(string column, bool descendsFirst) =>
        string.Equals(Column, column, StringComparison.Ordinal)
            ? this with { Descending = !Descending }
            : this with { Column = column, Descending = descendsFirst };

    /// <summary>The shape with a new filter.</summary>
    /// <param name="filter">What is in the box.</param>
    /// <returns>The new shape.</returns>
    public ListShape Narrowed(string? filter) => this with { Filter = filter ?? "" };

    /// <summary>Whether one field matches what was typed.</summary>
    /// <param name="fields">Everything on the row worth matching.</param>
    /// <returns><see langword="true"/> where the row survives the filter.</returns>
    /// <remarks>
    /// Case-insensitive and a substring, because the thing being typed is half a name remembered
    /// imperfectly. Every field on the row is searched rather than one: somebody looking for a
    /// container by the port it publishes should not have to know that ports are not the name column.
    /// </remarks>
    public bool Keeps(params string?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (!IsFiltered)
        {
            return true;
        }

        var needle = Filter.Trim();
        return fields.Any(field =>
            field is not null && field.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>What an empty list says when a filter is what emptied it.</summary>
    /// <returns>The headline and the detail, or null where the filter is not the reason.</returns>
    /// <remarks>
    /// The third empty state. "No containers" and "nothing matched `api`" are different answers, and
    /// only one of them is fixed by clearing a box — which is why it says what was typed rather than
    /// leaving somebody to look for the reason their machine appears to be empty.
    /// </remarks>
    public (string Headline, string Detail)? EmptyBecauseFiltered(string plural) =>
        IsFiltered
            ? ($"No {plural} match “{Filter.Trim()}”",
               "Clear the filter to see everything, or type less of the name.")
            : null;
}
