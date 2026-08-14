namespace FreeWilly.Core.Engine;

/// <summary>
/// The user's own <c>DOCKER_CONFIG</c>, which is what makes <c>docker compose</c> a subcommand in a
/// shell this process did not start (DD124).
/// </summary>
/// <remarks>
/// <b>The plugins were always there; nothing pointed the CLI at them.</b> DD73 and DD74 place Compose
/// and Buildx under <see cref="EnginePaths.PluginsDirectory"/>, and the CLI looks for a plugin in
/// <c>$DOCKER_CONFIG/cli-plugins</c> and nowhere else this project may write. <see
/// cref="Agent.BundledComposeCli"/> sets the variable on its own child, so the bundled
/// <c>do compose</c> works — but an <c>ant</c>, a <c>make</c> or a hand-typed <c>docker compose up</c>
/// is not that child, and got <c>unknown flag: --build</c>: with no plugin found, <c>compose</c> is
/// not a subcommand and the CLI parses the flag at its root. The same silence turned every
/// <c>docker build</c> into the legacy builder, because Buildx is found the same way.
///
/// <para><b>Which is why this reverses DD73's "not this tool's to do".</b> That comment left the
/// variable to the user and the install printed a sentence about it. A sentence is friction, and the
/// product this one is measured against sets its own plugins up without asking — so the rule now has
/// an owner, and this is it.</para>
///
/// <para><b>Owned only where the CLI is on PATH.</b> <c>DOCKER_CONFIG</c> is read by <i>every</i>
/// <c>docker.exe</c> a shell runs, not just this install's, and it carries <c>config.json</c>, the
/// contexts and the <c>docker login</c> credentials with it. Pointing it here is honest when this
/// install owns the <c>docker</c> command — which is precisely what the PATH entry means, and what
/// <see cref="Preflight.Windows.RivalEngineProbe"/> refuses to let a rival share. A user who declined
/// that checkbox said "leave my command line alone", and this leaves it alone.</para>
///
/// <para><b>The deciding is separated from the writing</b>, the same way the rival probe splits them:
/// <see cref="NeedsWriting"/> and <see cref="OwnsTheDockerCommand"/> are pure functions of strings a
/// test can hand them, because the registry state they would otherwise need is not something a test
/// may create on a developer's machine.</para>
/// </remarks>
public sealed class DockerConfigEntry
{
    /// <summary>The variable that tells the Docker CLI which config directory to read.</summary>
    public const string Variable = "DOCKER_CONFIG";

    /// <summary>The variable the PATH entry lands in, read to decide whether this is ours to set.</summary>
    private const string PathVariable = "PATH";

    private readonly EnginePaths _paths;

    /// <summary>Construct against the install this machine has.</summary>
    public DockerConfigEntry()
        : this(new EnginePaths())
    {
    }

    /// <summary>Construct against a layout, which is what says where the config directory is.</summary>
    /// <param name="paths">The install this speaks for.</param>
    public DockerConfigEntry(EnginePaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>What the variable has to say for the placed plugins to be found.</summary>
    public string Wanted => _paths.ConfigDirectory;

    /// <summary>
    /// Whether <paramref name="current"/> already says <paramref name="wanted"/>, trailing separator
    /// and casing aside.
    /// </summary>
    /// <remarks>
    /// Case-insensitively and without a trailing slash, because Windows paths compare that way and a
    /// user who typed the same directory with a backslash on the end has nothing wrong with their
    /// environment. Writing anyway would be a registry write and a <c>WM_SETTINGCHANGE</c> broadcast
    /// at every logon, for no change.
    /// </remarks>
    /// <param name="current">What the user's environment says now, or <see langword="null"/>.</param>
    /// <param name="wanted">What it has to say.</param>
    /// <returns><see langword="true"/> when the variable has to be written.</returns>
    public static bool NeedsWriting(string? current, string wanted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wanted);

        return string.IsNullOrWhiteSpace(current)
            || !Trimmed(current).Equals(Trimmed(wanted), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether this install's <c>bin</c> is on <paramref name="userPath"/> — the signal that the
    /// <c>docker</c> a shell resolves is this one, and that the variable is ours to point.
    /// </summary>
    /// <remarks>
    /// The user's <c>PATH</c> and not the process's: the process copy inherits the machine's entries
    /// and whatever the parent shell exported, and neither answers "did this user accept the
    /// installer's PATH entry".
    ///
    /// <para>Split on the separator rather than searched as text, so <c>\FreeWilly\bin</c> is not
    /// matched inside <c>\FreeWilly\bin2</c> or <c>\FreeWilly\bin\nested</c> — the guard
    /// <c>PathEntryMissing</c> makes in the installer, which is the other half of this rule. Each
    /// entry is then trimmed the same way the value is, because a <c>PATH</c> carrying a trailing
    /// separator or a trailing slash is ordinary and a rule that missed those would decline to fix
    /// exactly the machines most likely to need it.</para>
    /// </remarks>
    /// <param name="userPath">The user's <c>PATH</c>, or <see langword="null"/>.</param>
    /// <param name="cliDirectory">The directory the installer puts on it.</param>
    /// <returns><see langword="true"/> when the entry is there.</returns>
    public static bool OwnsTheDockerCommand(string? userPath, string cliDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliDirectory);

        if (string.IsNullOrWhiteSpace(userPath))
        {
            return false;
        }

        var wanted = Trimmed(cliDirectory);

        return userPath
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => Trimmed(entry).Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Point the user's <c>DOCKER_CONFIG</c> at this install's config directory, if that is this
    /// install's to do and is not already so.
    /// </summary>
    /// <remarks>
    /// Called at tray startup, so an install made before DD124 is repaired without a reinstall and a
    /// variable something else cleared comes back. The installer writes the same value at the same
    /// time it writes the PATH entry; this is the half that reaches a machine the installer has
    /// already left.
    ///
    /// <para>.NET's user-target write is the whole implementation: it writes
    /// <c>HKCU\Environment</c> and broadcasts <c>WM_SETTINGCHANGE</c> itself, which is what makes a
    /// shell started afterwards see the value. Already-running shells keep the environment they were
    /// started with — there is no mechanism that reaches those, which is why the installer writes it
    /// too rather than leaving this as the only writer.</para>
    /// </remarks>
    /// <returns>What was done, for a caller that logs.</returns>
    public DockerConfigOutcome Ensure()
    {
        string? path;
        string? current;
        try
        {
            path = Environment.GetEnvironmentVariable(PathVariable, EnvironmentVariableTarget.User);
            current = Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException)
        {
            // A policy that forbids reading the user's environment forbids writing it too. The tray
            // is not the place to argue with that, and the bundled compose call is unaffected.
            return DockerConfigOutcome.Refused;
        }

        if (!OwnsTheDockerCommand(path, _paths.CliDirectory))
        {
            return DockerConfigOutcome.NotOurs;
        }

        if (!NeedsWriting(current, Wanted))
        {
            return DockerConfigOutcome.AlreadyRight;
        }

        try
        {
            Environment.SetEnvironmentVariable(
                Variable, Wanted, EnvironmentVariableTarget.User);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException)
        {
            return DockerConfigOutcome.Refused;
        }

        return DockerConfigOutcome.Written;
    }

    private static string Trimmed(string value) =>
        value.Trim().TrimEnd('\\', '/');
}

/// <summary>What <see cref="DockerConfigEntry.Ensure"/> did.</summary>
public enum DockerConfigOutcome
{
    /// <summary>The variable already pointed at this install's config directory.</summary>
    AlreadyRight,

    /// <summary>It was written, so a shell started from now on finds the placed plugins.</summary>
    Written,

    /// <summary>
    /// This install is not on the user's PATH, so the <c>docker</c> their shell resolves is not this
    /// one and the variable is not this one's to point.
    /// </summary>
    NotOurs,

    /// <summary>The environment could not be read or written, and the tray does not insist.</summary>
    Refused,
}
