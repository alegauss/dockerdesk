namespace FreeWilly.Core.Engine;

/// <summary>
/// Where everything this tool owns lives, and what its distribution is called. One root under
/// <c>%LOCALAPPDATA%</c>, because a per-user install is what reaches a managed laptop with no
/// administrator prompt.
/// </summary>
/// <remarks>
/// <b>These two names are the only ones the rename could not overwrite (DD55).</b> Every other
/// spelling in this tree is text in a build; a root and a distribution name are state on somebody's
/// machine. The distribution holds every image, container and volume the user created, and
/// <c>distro</c>, <c>downloads</c> and <c>bin</c> hang off the root — <c>bin</c> being what the
/// installer put on <c>PATH</c>. A build that simply spelled them the new way would start an empty
/// engine beside a full one and report nothing installed on a machine that has everything.
///
/// <para><b>The decision this task records: adopt in place, and never re-import.</b> The alternative
/// was to export the distribution and import it under the new name, and it is wrong twice over. It
/// copies gigabytes to change a label, and it has a failure mode in the middle that loses the lot.
/// The root cannot move for the same reason from the other end: <c>distro</c> is the BasePath WSL
/// registered the distribution at, so moving the directory orphans the distribution exactly as
/// surely as renaming it would.</para>
///
/// <para>So an install made before the rename keeps both old names for as long as it lives, a fresh
/// install gets both new ones, and which of the two this is gets resolved once — from what is on
/// disk and what WSL has registered, never from a stored flag that could disagree with either. The
/// old spellings live in <see cref="Legacy"/> and nowhere else, so the next reader meets them as
/// history rather than as the current name.</para>
///
/// <para>The comment this replaced said an owned distribution makes the uninstall exactly one
/// command, and that stays true: the uninstall unregisters <see cref="DistributionName"/>, which is
/// whichever name this install actually owns.</para>
/// </remarks>
public sealed class EnginePaths
{
    /// <summary>
    /// The distribution a fresh install creates. Deliberately not the user's own: an
    /// <c>apk upgrade</c> or a <c>wsl --unregister</c> they ran for another reason would take the
    /// engine with it.
    /// </summary>
    public const string CurrentDistribution = "freewilly";

    /// <summary>The directory a fresh install owns under <c>%LOCALAPPDATA%</c>.</summary>
    public const string CurrentRootName = "FreeWilly";

    /// <summary>What an install made before the rename left on a machine (DD55).</summary>
    /// <remarks>
    /// One place, so a reader who meets <c>dockerdesk</c> in a WSL listing or
    /// <c>%LOCALAPPDATA%\DockerDesk</c> in a path can find out in one search that it is an adopted
    /// install rather than a spelling somebody forgot to change.
    /// </remarks>
    public static class Legacy
    {
        /// <summary>The distribution an install made before the rename registered.</summary>
        public const string Distribution = "dockerdesk";

        /// <summary>The directory an install made before the rename rooted itself in.</summary>
        public const string RootName = "DockerDesk";
    }

    /// <summary>Construct the layout under an explicit root.</summary>
    /// <param name="root">The directory this tool owns.</param>
    /// <param name="distributionName">
    /// The distribution this install owns. Defaults to what a fresh install creates.
    /// </param>
    public EnginePaths(string root, string distributionName = CurrentDistribution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(distributionName);
        Root = root;
        DistributionName = distributionName;
    }

    /// <summary>Construct the layout this machine actually has, adopting an older install.</summary>
    public EnginePaths()
        : this(
            RootFor(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Directory.Exists),
            DistributionFor(Wsl.RegisteredDistributions()))
    {
    }

    /// <summary>Which root this install owns.</summary>
    /// <param name="localAppData">The user's <c>%LOCALAPPDATA%</c>.</param>
    /// <param name="exists">Whether a directory is there — a parameter so this is testable.</param>
    /// <returns>The directory to use.</returns>
    /// <remarks>
    /// A root is adopted for holding an install, not for existing. The directory alone is not
    /// evidence: this one writes <c>window.json</c> into the root the first time a window closes, so
    /// a machine that only ever opened the window has a legacy directory with a preference file in it
    /// and no engine — and adopting that would point a fresh install's <c>bin</c> at a folder with
    /// nothing in it. <c>distro</c> or <c>downloads</c> is the same evidence
    /// <c>build\installer.iss</c> asks for before it offers to delete anything, and the two agreeing
    /// is what keeps install and uninstall talking about one directory.
    ///
    /// <para>The current name wins where both hold an install, which is the only ordering that
    /// converges: preferring the legacy one there would make the adoption permanent by accident on a
    /// machine where the new install is the real one.</para>
    /// </remarks>
    public static string RootFor(string localAppData, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        ArgumentNullException.ThrowIfNull(exists);

        var current = Path.Combine(localAppData, CurrentRootName);
        if (HoldsAnInstall(current, exists))
        {
            return current;
        }

        var legacy = Path.Combine(localAppData, Legacy.RootName);
        return HoldsAnInstall(legacy, exists) ? legacy : current;
    }

    /// <summary>Whether a root holds an install rather than merely being a directory.</summary>
    private static bool HoldsAnInstall(string root, Func<string, bool> exists) =>
        exists(Path.Combine(root, "distro")) || exists(Path.Combine(root, "downloads"));

    /// <summary>Which distribution this install owns.</summary>
    /// <param name="registered">Every distribution WSL has registered.</param>
    /// <returns>The name to drive, terminate and unregister.</returns>
    /// <remarks>
    /// Asked of WSL rather than derived from the root, because the two can disagree: a root left
    /// behind by an uninstall that kept the user's data has no distribution under it, and a
    /// distribution survives a root somebody deleted by hand.
    /// </remarks>
    public static string DistributionFor(IEnumerable<string> registered)
    {
        ArgumentNullException.ThrowIfNull(registered);
        var names = registered.ToList();

        if (Has(names, CurrentDistribution))
        {
            return CurrentDistribution;
        }

        return Has(names, Legacy.Distribution) ? Legacy.Distribution : CurrentDistribution;

        static bool Has(List<string> names, string wanted) =>
            names.Any(name => name.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The directory this tool owns.</summary>
    public string Root { get; }

    /// <summary>
    /// The WSL2 distribution this install owns — <see cref="CurrentDistribution"/>, or
    /// <see cref="Legacy.Distribution"/> where an older install is being adopted.
    /// </summary>
    public string DistributionName { get; }

    /// <summary>Whether the distribution being driven is one registered before the rename.</summary>
    public bool DistributionIsLegacy =>
        string.Equals(DistributionName, Legacy.Distribution, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the root being used is one an install made before the rename created.</summary>
    public bool RootIsLegacy =>
        string.Equals(
            Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)),
            Legacy.RootName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether either name is still the old one, which is what makes this an adopted install.
    /// </summary>
    /// <remarks>
    /// Either, and not both. An uninstall that kept the user's data leaves a root with no
    /// distribution under it, and a distribution outlives a root somebody deleted by hand — so the
    /// two are asked separately by anything that reports which is which.
    /// </remarks>
    public bool IsAdopted => DistributionIsLegacy || RootIsLegacy;

    /// <summary>Verified artefacts, kept so a repeated install does not download again.</summary>
    public string Downloads => Path.Combine(Root, "downloads");

    /// <summary>Where the distribution's virtual disk is imported to.</summary>
    public string Distribution => Path.Combine(Root, "distro");

    /// <summary>
    /// The directory holding <c>docker.exe</c> — and the one thing an installer needs from here:
    /// this is the path that goes on the user's PATH, and putting it there is the installer's job,
    /// not this one's.
    /// </summary>
    public string CliDirectory => Path.Combine(Root, "bin");

    /// <summary>The Windows <c>docker</c> CLI.</summary>
    public string DockerCli => Path.Combine(CliDirectory, "docker.exe");

    /// <summary>
    /// The handful of values the window remembers between runs — where it was, and what was being
    /// read (DD39).
    /// </summary>
    /// <remarks>
    /// In the root rather than under a directory of its own, and not created by <see cref="Create"/>:
    /// it is written when a window closes and its absence is the answer for a first run.
    /// </remarks>
    public string WindowState => Path.Combine(Root, "window.json");

    /// <summary>Create every directory this layout names. Idempotent.</summary>
    public void Create()
    {
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Distribution);
        Directory.CreateDirectory(CliDirectory);
    }
}
