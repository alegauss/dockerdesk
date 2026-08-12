namespace DockerDesk.Core.Preflight.Windows;

/// <summary>Finds container engines already installed, by the things this one would take over.</summary>
internal static class RivalEngineProbe
{
    /// <summary>The pipe an Engine API client connects to on Windows.</summary>
    internal const string EnginePipeName = "docker_engine";

    /// <summary>Read what is already here.</summary>
    /// <returns>One entry per engine found, each carrying the evidence it was found by.</returns>
    internal static IReadOnlyList<RivalEngine> Read()
    {
        var found = new List<RivalEngine>();

        AddIfPresent(found, "Docker Desktop", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker", "Docker", "Docker Desktop.exe"));

        AddIfPresent(found, "Rancher Desktop", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Rancher Desktop", "Rancher Desktop.exe"));

        // The pipe is the fact that actually matters: an engine nobody installed through a
        // shortcut this probe knows about still owns the one endpoint a client can reach.
        if (EnginePipeExists() && found.Count == 0)
        {
            found.Add(new RivalEngine(
                "an unidentified engine", $@"\\.\pipe\{EnginePipeName} is open"));
        }

        return found;
    }

    /// <summary>Whether something is listening on the Engine API pipe.</summary>
    /// <returns><see langword="true"/> when the pipe exists.</returns>
    internal static bool EnginePipeExists()
    {
        try
        {
            // Enumeration rather than File.Exists: the pipe filesystem answers a directory listing
            // reliably and answers an existence probe differently depending on the pipe's ACL.
            return Directory
                .EnumerateFileSystemEntries(@"\\.\pipe\")
                .Any(entry => Path.GetFileName(entry)
                    .Equals(EnginePipeName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static void AddIfPresent(List<RivalEngine> found, string name, string path)
    {
        if (File.Exists(path))
        {
            found.Add(new RivalEngine(name, path));
        }
    }
}
