using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeWilly.Core.Api;

namespace FreeWilly.Core.Agent;

/// <summary>One thing a reclaim would remove, or deliberately would not.</summary>
/// <param name="Kind">container or volume.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Detail">What a reader needs to decide, in one phrase.</param>
public sealed record ReclaimItem(string Kind, string Name, string Detail);

/// <summary>What a reclaim is about to do, and the token that says this list was seen.</summary>
/// <param name="Session">The session it is scoped to.</param>
/// <param name="Removing">What goes.</param>
/// <param name="Keeping">What is this session's and stays anyway, with the reason.</param>
/// <param name="Token">A fingerprint of <paramref name="Removing"/>.</param>
public sealed record ReclaimPlan(
    string Session,
    IReadOnlyList<ReclaimItem> Removing,
    IReadOnlyList<ReclaimItem> Keeping,
    string Token);

/// <summary>
/// A scoped undo, and the token that keeps it from acting on a list nobody saw.
/// </summary>
/// <remarks>
/// DD29. What an agent created is indistinguishable from what the user created, so the only cleanup on
/// offer is <c>prune</c> — scoped to the machine, unable to tell this afternoon's scaffolding from the
/// database somebody has been filling since March, and therefore the one command nobody delegates. The
/// leftovers stay, and the next session starts on a machine with a history it did not write.
///
/// <para><see cref="SessionLabel"/> supplies the scope. What is left is the part that makes a delete
/// safe to hand over: a plan is printed before anything is removed, and a destructive call takes a
/// <b>confirm token computed over that list</b>. Right is the token and the list together. Wrong — a
/// container started in between, one removed by hand, a plan from ten minutes ago — is a refusal that
/// names what would go <i>now</i>, so a stale plan costs a second call rather than something that
/// arrived after it was printed.</para>
///
/// <para><b>Volumes stay the exception this is loudest about.</b> They are named in the plan and not
/// removed, because a container comes back from an image and a volume does not come back from anything.
/// <c>--volumes</c> moves them into the removal set, and because the token is computed over that set,
/// a token issued for the containers cannot be replayed to take the data with them.</para>
/// </remarks>
public static class Reclaim
{
    /// <summary>What a confirm token starts with.</summary>
    /// <remarks>
    /// Its own prefix, distinct from the <c>c:</c> of a context cursor and the <c>t:</c> of a log
    /// cursor. The three are different currencies and one of them authorises a delete, so a caller that
    /// pastes the wrong one is told which it pasted rather than being met with a checksum mismatch.
    /// </remarks>
    public const string TokenPrefix = "k:";

    /// <summary>The container kind, as it appears in a plan.</summary>
    public const string Container = "container";

    /// <summary>The volume kind.</summary>
    public const string Volume = "volume";

    /// <summary>Why a volume is kept, said once and in full.</summary>
    public const string VolumeReason = "a container comes back from its image; a volume comes back from nothing";

    /// <summary>What this session made, and what removing it would take.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="containers">Every container on the engine.</param>
    /// <param name="volumes">Every volume on the engine.</param>
    /// <param name="includeVolumes">Whether volumes join the removal set.</param>
    /// <returns>The plan.</returns>
    public static ReclaimPlan Plan(
        string session,
        IReadOnlyList<ContainerSummary> containers,
        IReadOnlyList<VolumeSummary> volumes,
        bool includeVolumes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session);
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(volumes);

        // Sorted, because a plan whose order came from the daemon would change its own token between two
        // calls that found exactly the same things.
        var mine = containers
            .Where(c => SessionLabel.Owns(c.Labels, session))
            .OrderBy(c => c.DisplayName, StringComparer.Ordinal)
            .Select(c => new ReclaimItem(
                Container,
                c.DisplayName,
                c.State + "  " + c.Image))
            .ToList();

        var data = volumes
            .Where(v => SessionLabel.Owns(v.Labels, session))
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .Select(v => new ReclaimItem(
                Volume,
                v.Name,
                v.UsageData is { RefCount: > 0 } usage
                    ? usage.RefCount.ToString(CultureInfo.InvariantCulture) + " container(s) mount it"
                    : "not mounted"))
            .ToList();

        var removing = includeVolumes ? [.. mine, .. data] : mine;
        return new ReclaimPlan(session, removing, includeVolumes ? [] : data, TokenFor(session, removing));
    }

    /// <summary>
    /// A fingerprint of exactly what would be removed.
    /// </summary>
    /// <remarks>
    /// Over the removal set and nothing else, which is what makes the guarantee narrow enough to be
    /// worth something: the token changes when the list changes, and it does not change because a log
    /// line arrived or a container this session does not own was started somewhere else.
    /// </remarks>
    /// <param name="session">The session id.</param>
    /// <param name="removing">What would go.</param>
    /// <returns>The token.</returns>
    public static string TokenFor(string session, IReadOnlyList<ReclaimItem> removing)
    {
        ArgumentNullException.ThrowIfNull(removing);

        var material = new StringBuilder(session).Append('\n');
        foreach (var item in removing)
        {
            material.Append(item.Kind).Append('\t').Append(item.Name).Append('\n');
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));
        return TokenPrefix + Convert.ToHexStringLower(digest)[..6];
    }

    /// <summary>The plan, for a reader who has to approve it.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ReclaimPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var text = new StringBuilder();
        text.Append("session  ").AppendLine(Describe(plan.Session));

        if (plan.Removing.Count == 0)
        {
            text.AppendLine("nothing to reclaim: this session created nothing that is still here");
        }
        else
        {
            text.Append("would remove  ")
                .AppendLine(Count(plan.Removing));
            foreach (var item in plan.Removing)
            {
                text.Append("  ").Append(item.Kind.PadRight(10)).Append(item.Name.PadRight(24))
                    .AppendLine(item.Detail);
            }
        }

        if (plan.Keeping.Count > 0)
        {
            // Loud on purpose, and above the confirm line rather than below it: this is the sentence a
            // caller has to have read before it pastes a token.
            text.Append("KEEPING  ").Append(Count(plan.Keeping)).Append("  ").AppendLine(VolumeReason);
            foreach (var item in plan.Keeping)
            {
                text.Append("  ").Append(item.Kind.PadRight(10)).Append(item.Name.PadRight(24))
                    .AppendLine(item.Detail);
            }

            text.AppendLine("  --volumes takes them too, and changes the token below.");
        }

        if (plan.Removing.Count > 0)
        {
            // The whole command, --volumes included where it applies: a confirm line that omitted the
            // flag would be copied, re-run without it, and refuse against a token it no longer matches.
            text.Append("confirm  dockerdesk do reclaim --session ").Append(plan.Session)
                .Append(plan.Removing.Any(i => string.Equals(i.Kind, Volume, StringComparison.Ordinal))
                    ? " --volumes --confirm "
                    : " --confirm ")
                .AppendLine(plan.Token);
        }

        return text.ToString();
    }

    /// <summary>
    /// The same list, for a caller that only asked what is there.
    /// </summary>
    /// <remarks>
    /// No token, because there is nothing to authorise, and no <c>KEEPING</c>, because nothing is being
    /// taken. What it does carry is the label's own claim: every row is here because it was stamped when
    /// it was made, not because its creation time falls in some window. A timestamp would put the user's
    /// own work in this list on any afternoon they happened to be working alongside.
    /// </remarks>
    /// <param name="plan">A plan built over everything this session owns.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string RenderChanges(ReclaimPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var text = new StringBuilder();
        text.Append("session  ").AppendLine(Describe(plan.Session));
        if (plan.Removing.Count == 0)
        {
            text.AppendLine("(nothing carries this session's label)");
            return text.ToString();
        }

        foreach (var item in plan.Removing)
        {
            text.Append(item.Kind.PadRight(10)).Append(item.Name.PadRight(24))
                .AppendLine(item.Detail);
        }

        text.Append("undo  dockerdesk do reclaim --session ").AppendLine(plan.Session);
        return text.ToString();
    }

    /// <summary>The plan as one object, for a caller that parses.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The document, ending in a newline.</returns>
    public static string RenderJson(ReclaimPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["session"] = plan.Session,
            ["derived"] = SessionLabel.IsDerived(plan.Session),
            ["removing"] = plan.Removing,
            ["keeping"] = plan.Keeping,
            ["keptBecause"] = plan.Keeping.Count > 0 ? VolumeReason : null,
            ["confirm"] = plan.Token,
        }) + Environment.NewLine;
    }

    /// <summary>
    /// The refusal for a token that does not match the list as it stands now.
    /// </summary>
    /// <remarks>
    /// It names what would go now rather than what changed, because the caller's next move is to approve
    /// this list or not, and a diff against a list it no longer has in front of it is one more thing to
    /// reconstruct. A wrong token and a stale token are the same refusal on purpose: both mean this call
    /// was authorised against something other than what is here.
    /// </remarks>
    /// <param name="plan">The plan as it stands.</param>
    /// <param name="given">The token the caller offered.</param>
    /// <returns>The refusal.</returns>
    public static AgentProblem Stale(ReclaimPlan plan, string given)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["session"] = plan.Session,
            ["given"] = given,
            ["now"] = plan.Token,
        };

        if (plan.Removing.Count > 0)
        {
            // In the facts and not only in the plan printed beneath, because --json is one document: a
            // caller that parses would otherwise get a refusal that says the list changed and never says
            // to what, which is the half that makes the next call a decision instead of an inquiry.
            facts["wouldRemoveNow"] = string.Join(
                ", ", plan.Removing.Select(i => i.Kind + ":" + i.Name));
        }

        return new AgentProblem(
            Type: "reclaim-not-confirmed",
            Status: 409,
            Title: plan.Removing.Count == 0
                ? "nothing here belongs to that session any more"
                : "that token was computed over a different list",
            Fix: plan.Removing.Count == 0
                ? "Nothing was removed. Run `dockerdesk read changes` to see what is there."
                : "Nothing was removed. Read the plan below and confirm it with " + plan.Token + ".",
            Facts: facts);
    }

    /// <summary>Whether a token authorises this exact plan.</summary>
    /// <param name="plan">The plan as it stands.</param>
    /// <param name="given">The token the caller offered.</param>
    /// <returns><see langword="true"/> where it matches.</returns>
    public static bool Confirms(ReclaimPlan plan, string? given)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return given is not null
               && plan.Removing.Count > 0
               && string.Equals(given, plan.Token, StringComparison.Ordinal);
    }

    /// <summary>A session id, said in a way that distinguishes a piece of work from a folder.</summary>
    private static string Describe(string session) =>
        SessionLabel.IsDerived(session)
            ? session + "  (derived from this directory: " + SessionLabel.Variable
                + " names one instead)"
            : session;

    private static string Count(IReadOnlyList<ReclaimItem> items)
    {
        var containers = items.Count(i => string.Equals(i.Kind, Container, StringComparison.Ordinal));
        var volumes = items.Count - containers;
        var parts = new List<string>(2);
        if (containers > 0)
        {
            parts.Add(containers.ToString(CultureInfo.InvariantCulture) + " container(s)");
        }

        if (volumes > 0)
        {
            parts.Add(volumes.ToString(CultureInfo.InvariantCulture) + " volume(s)");
        }

        return string.Join(", ", parts);
    }
}
