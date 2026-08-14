using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// A list that is reconciled rather than replaced, so a row can say it arrived (DD70).
/// </summary>
/// <remarks>
/// Every page used to assign <c>ItemsSource</c> outright on each refresh, which is correct and says
/// nothing: WPF throws the containers away and builds them again, so a container that appeared and
/// one that was always there are drawn identically. Fading on that signal would flash the whole list
/// on every poll from the event stream — louder than no motion at all, and the opposite of Fluent's
/// rule that motion answers "did that change, or did I misread it?".
///
/// <para>So the collection persists and each refresh is a diff against it, keyed by the row's own
/// id. Only what joined fades in. A row that left is held for the length of its fade and then
/// removed, because an assignment cannot express "gone, but not yet" — and without that a departing
/// row simply vanishes, which is the same silence in the other direction.</para>
///
/// <para><b>Realised containers only.</b> The fade is applied by looking the row up through the
/// <see cref="ItemContainerGenerator"/>, so a row scrolled out of view is not animated — which is
/// right, and is also why this cannot hang the fade off a container's <c>Loaded</c>: virtualisation
/// recycles containers, so that fires on scrolling and would fade rows that did not change.</para>
/// </remarks>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="host">The list this drives.</param>
/// <param name="key">A row's identity, stable across refreshes.</param>
internal sealed class LiveRows<T>(ItemsControl host, Func<T, string> key)
    where T : class
{
    /// <summary>How long a row takes to arrive or leave.</summary>
    /// <remarks>
    /// Short. The fade says where to look, and a reader who has to wait for it has been told
    /// something slower than the change itself.
    /// </remarks>
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(180);

    private readonly ObservableCollection<T> _shown = [];
    private readonly HashSet<string> _leaving = new(StringComparer.Ordinal);
    private bool _bound;

    /// <summary>Draw this list, animating only what changed.</summary>
    /// <param name="next">The rows as they should now be, in order.</param>
    internal void Show(IReadOnlyList<T> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!_bound)
        {
            host.ItemsSource = _shown;
            _bound = true;

            // The first draw is the whole list, and none of it is news: everything is arriving
            // because the page just opened. Fading all of it would be the flash this exists to
            // avoid, so the first pass is silent.
            foreach (var row in next)
            {
                _shown.Add(row);
            }

            return;
        }

        var wanted = next.Select(key).ToHashSet(StringComparer.Ordinal);

        // Leaving first, so the indexes below are computed against what is really there. A row
        // already on its way out is left alone rather than started again.
        foreach (var row in _shown.ToList())
        {
            var id = key(row);
            if (wanted.Contains(id) || !_leaving.Add(id))
            {
                continue;
            }

            Leave(row, id);
        }

        var joined = new List<T>();
        for (var i = 0; i < next.Count; i++)
        {
            var id = key(next[i]);
            var at = IndexOf(id);

            if (at < 0)
            {
                _shown.Insert(Math.Min(i, _shown.Count), next[i]);
                joined.Add(next[i]);
                continue;
            }

            if (at != i && i < _shown.Count)
            {
                _shown.Move(at, i);
            }

            // Same row, new values — the rows are immutable, so the instance is replaced. No fade:
            // nothing arrived, a cell changed, and the cell says so itself.
            //
            // Value equality, not reference: `Shaped` builds fresh instances on every refresh, so a
            // reference check would replace every row on every poll — regenerating every container,
            // which is the wholesale rebuild this class exists to stop doing.
            var here = IndexOf(id);
            if (here >= 0 && !Equals(_shown[here], next[i]))
            {
                _shown[here] = next[i];
            }
        }

        Arrive(joined);
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (string.Equals(key(_shown[i]), id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Fade the rows that were not here last time.</summary>
    /// <remarks>
    /// After layout, because a row inserted a moment ago has no container yet: the generator makes
    /// one during the next render pass, and asking before that returns null for every one of them.
    /// </remarks>
    private void Arrive(List<T> joined)
    {
        if (joined.Count == 0 || !Motion.Allowed)
        {
            return;
        }

        _ = host.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () =>
            {
                foreach (var row in joined)
                {
                    if (host.ItemContainerGenerator.ContainerFromItem(row) is UIElement container)
                    {
                        container.BeginAnimation(
                            UIElement.OpacityProperty,
                            new DoubleAnimation(0, 1, new Duration(Fade)));
                    }
                }
            });
    }

    /// <summary>Hold a departing row for the length of its fade, then drop it.</summary>
    /// <remarks>
    /// With motion off it goes immediately, which is the end state rather than a slower version of
    /// the same thing — the rule <c>Ui/Breathing.cs</c> already follows and the one that keeps
    /// <c>--capture-window</c> byte-identical.
    /// </remarks>
    private void Leave(T row, string id)
    {
        if (!Motion.Allowed
            || host.ItemContainerGenerator.ContainerFromItem(row) is not UIElement container)
        {
            Drop(row, id);
            return;
        }

        var fade = new DoubleAnimation(1, 0, new Duration(Fade));
        fade.Completed += (_, _) =>
        {
            // The value is put back before the row goes, because the container is recycled: a
            // virtualising list hands the same element to a different row, and one left at zero
            // would make that row invisible.
            container.BeginAnimation(UIElement.OpacityProperty, null);
            container.Opacity = 1;
            Drop(row, id);
        };

        container.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Drop(T row, string id)
    {
        _ = _shown.Remove(row);
        _ = _leaving.Remove(id);
    }
}
