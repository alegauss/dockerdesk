namespace DockerDesk.Core.Engine;

/// <summary>
/// Where everything this tool owns lives. One root under <c>%LOCALAPPDATA%</c>, because a per-user
/// install is what reaches a managed laptop with no administrator prompt.
/// </summary>
public sealed class EnginePaths
{
    /// <summary>
    /// The name of the WSL2 distribution this tool owns. Fixed, and deliberately not the user's:
    /// an <c>apt upgrade</c> or a <c>wsl --unregister</c> they ran for another reason would take
    /// the engine with it, and an owned distribution makes the uninstall exactly one command.
    /// </summary>
    public const string DistributionName = "dockerdesk";

    /// <summary>Construct the layout under an explicit root.</summary>
    /// <param name="root">The directory this tool owns.</param>
    public EnginePaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    /// <summary>Construct the layout under <c>%LOCALAPPDATA%\DockerDesk</c>.</summary>
    public EnginePaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DockerDesk"))
    {
    }

    /// <summary>The directory this tool owns.</summary>
    public string Root { get; }

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

    /// <summary>Create every directory this layout names. Idempotent.</summary>
    public void Create()
    {
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Distribution);
        Directory.CreateDirectory(CliDirectory);
    }
}
