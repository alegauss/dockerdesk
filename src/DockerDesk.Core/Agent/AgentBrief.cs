using System.Text;

namespace DockerDesk.Core.Agent;

/// <summary>
/// What a session should start knowing, generated from the machine rather than maintained by hand.
/// </summary>
/// <remarks>
/// DD32. A capability nobody discovers is one nobody uses: an agent meeting this machine reaches for
/// <c>docker</c> because that is what it knows is there, and the discovery cost is otherwise paid once
/// per session forever.
///
/// <para><b>Generated, so it cannot rot.</b> A project file describing the machine is written once and
/// then quietly stops being true — the container it names was renamed in March. This one is re-run, and
/// the part that could go stale is the part that came from the live engine a moment ago.</para>
///
/// <para><b>It names verbs and defers.</b> Every sentence explaining what a verb does lives in
/// <c>--help</c>, which is one copy and the one a caller already has. Two descriptions of one surface
/// drift, and the one loaded every session is the one that drifts unnoticed — so the verb list here is
/// handed in from the registry rather than written down, and nothing here explains a verb.</para>
///
/// <para><b>No timestamp.</b> Re-running it on an unchanged machine gives a byte-identical file, so it
/// sits in a repository without producing a diff that says nothing. "Re-run to refresh" is the line
/// that would otherwise have been a date.</para>
/// </remarks>
public static class AgentBrief
{
    /// <summary>The allowlist line the whole read/do split exists to make grantable.</summary>
    public const string AllowEntry = "Bash(dockerdesk read:*)";

    /// <summary>Write the brief.</summary>
    /// <param name="facts">The machine, as the context pack gathered it.</param>
    /// <param name="verbs">Every verb there is, from the registry.</param>
    /// <returns>The file, ending in a newline.</returns>
    public static string Render(ContextFacts facts, IReadOnlyList<string> verbs)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(verbs);

        var text = new StringBuilder();
        text.AppendLine("# Docker on this machine");
        text.AppendLine();
        text.AppendLine("This machine runs Docker through DockerDesk. **Reach for `dockerdesk read`");
        text.AppendLine("before `docker`**: the read half mutates nothing and two guards in its build");
        text.AppendLine("hold that, so one allowlist line covers all of it.");
        text.AppendLine();
        text.AppendLine("```jsonc");
        text.AppendLine("// .claude/settings.json");
        text.Append("\"allow\": [\"").Append(AllowEntry).AppendLine("\"]");
        text.AppendLine("```");
        text.AppendLine();
        text.AppendLine("```");
        foreach (var verb in verbs)
        {
            text.AppendLine(verb);
        }

        text.AppendLine("```");
        text.AppendLine();
        text.AppendLine("`dockerdesk --help` says what each does. Nothing is repeated here: two");
        text.AppendLine("descriptions of one surface drift, and this is the copy that would drift");
        text.AppendLine("unnoticed.");
        text.AppendLine();
        text.AppendLine("## What was here when this was generated");
        text.AppendLine();
        text.AppendLine("Re-run `dockerdesk read context --as brief` to refresh it.");
        text.AppendLine();
        text.AppendLine("```");

        // The pack itself, unchanged and under its own ceiling. Rendering the machine a second way
        // here would be the drift this file is careful about everywhere else.
        text.Append(ContextPack.Render(facts));
        text.AppendLine("```");

        return text.ToString();
    }
}
