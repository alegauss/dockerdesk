namespace DockerDesk.Tray.Cli;

/// <summary>Which of this executable's faces a command line asked for.</summary>
public enum Surface
{
    /// <summary>The tray icon, and a window if it was asked for.</summary>
    Tray,

    /// <summary>The preflight report, on the console.</summary>
    Preflight,

    /// <summary>One of the engine's modes, on the console.</summary>
    Engine,

    /// <summary>What this build calls itself, on the console.</summary>
    Version,

    /// <summary>Every verb there is, on the console.</summary>
    Help,

    /// <summary>The agent surface: <c>read</c> and <c>do</c> (DD24).</summary>
    Agent,

    /// <summary>A PNG of the window, rendered rather than photographed.</summary>
    CaptureWindow,

    /// <summary>A verb this executable does not have.</summary>
    Unknown,
}

/// <summary>
/// What the first argument means.
/// </summary>
/// <remarks>
/// One executable, so one place that decides. Before this there were three — a tray, a preflight and
/// an engine — and the installer had three files to ship, three signatures to buy and three copies
/// of the runtime to carry; the tray also had to find <c>dockerdesk-engine.exe</c> beside itself,
/// which is a way to be broken by a half-finished copy.
///
/// A pure function of the arguments and nothing else: it reaches no console, starts no window and
/// touches no machine, which is what lets every route be asserted on rather than described.
/// </remarks>
public static class CommandLine
{
    /// <summary>The name this executable is shipped as.</summary>
    public const string ExecutableName = "DockerDesk.exe";

    /// <summary>The verb that asks for the preflight.</summary>
    public const string PreflightVerb = "--preflight";

    /// <summary>The verb that opens the window along with the tray.</summary>
    public const string WindowVerb = "--window";

    /// <summary>The verb that renders the window to a PNG without showing it (DD22).</summary>
    public const string CaptureWindowVerb = "--capture-window";

    /// <summary>
    /// The engine's modes, exactly as <see cref="EngineCommand"/> reads them.
    /// </summary>
    /// <remarks>
    /// Public so a test can hold this list against the one the engine's own help prints. Two lists
    /// of verbs is how a verb becomes reachable in one of them and not the other.
    /// </remarks>
    public static readonly IReadOnlySet<string> EngineVerbs =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "--plan",
            "--acquire",
            "--provision",
            "--run",
            "--stop",
            "--status",
            "--api",
            "--watch",
            "--autostart",
        };

    /// <summary>What a command line asked for.</summary>
    /// <param name="Surface">Which face of this executable.</param>
    /// <param name="OpenWindow">Whether the tray should show the window straight away.</param>
    /// <param name="Arguments">What the surface itself should read, the verb included.</param>
    public sealed record Route(Surface Surface, bool OpenWindow, string[] Arguments);

    /// <summary>Read a command line.</summary>
    /// <param name="arguments">Everything after the executable's own name.</param>
    /// <returns>The route. Never null, and never throws.</returns>
    public static Route Of(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length == 0)
        {
            return new Route(Surface.Tray, OpenWindow: false, []);
        }

        var first = arguments[0];

        if (EngineVerbs.Contains(first))
        {
            return new Route(Surface.Engine, OpenWindow: false, arguments);
        }

        // The agent surface, and it is a bare word rather than a flag on purpose: an allowlist matches
        // the literal prefix `dockerdesk read`, and a flag would put the two halves in one namespace
        // again — which is the whole thing DD24 exists to undo.
        if (string.Equals(first, AgentSurface.ReadVerb, StringComparison.Ordinal)
            || string.Equals(first, AgentSurface.DoVerb, StringComparison.Ordinal))
        {
            return new Route(Surface.Agent, OpenWindow: false, arguments);
        }

        if (string.Equals(first, CaptureWindowVerb, StringComparison.Ordinal))
        {
            // The verb is dropped: what follows is the output path and an optional tab.
            return new Route(Surface.CaptureWindow, OpenWindow: false, arguments[1..]);
        }

        if (string.Equals(first, PreflightVerb, StringComparison.Ordinal))
        {
            // The verb itself is dropped: what follows is the preflight's own argument list, and it
            // would refuse --preflight as an argument it does not have.
            return new Route(Surface.Preflight, OpenWindow: false, arguments[1..]);
        }

        // Case-insensitive and position-independent, because this is the one argument a Windows
        // shortcut carries and a shortcut is edited by hand.
        if (arguments.Contains(WindowVerb, StringComparer.OrdinalIgnoreCase))
        {
            return arguments.Length == 1
                ? new Route(Surface.Tray, OpenWindow: true, [])
                : new Route(Surface.Unknown, OpenWindow: false, arguments);
        }

        return first switch
        {
            "--version" => new Route(Surface.Version, OpenWindow: false, []),
            "-h" or "--help" => new Route(Surface.Help, OpenWindow: false, []),
            _ => new Route(Surface.Unknown, OpenWindow: false, arguments),
        };
    }

    /// <summary>Every verb, in one place, for the console's own help.</summary>
    public static string HelpText =>
        $"""
        {ExecutableName} — install and drive Docker on Windows.

        With no arguments it is the tray icon. Everything else is a console verb and prints
        into the terminal it was started from.

          read <verb>      the agent surface, which mutates nothing
          do <verb>        the agent surface that does
          {WindowVerb}         the tray, with the window open straight away
          {CaptureWindowVerb} <out.png> [tab]
                           render the window to a PNG off-screen, photographing nothing else

          {PreflightVerb}      what this machine can host; add --json for an installer
          --plan           the pinned versions, digests and paths; reaches nothing
          --acquire        download and verify every artefact, and stop
          --provision      acquire, import the distribution, install the engine

          --run            start the engine and serve the pipe until Ctrl+C
          --stop           stop the engine and terminate the distribution
          --status         what the engine is doing, by asking it
          --api            version and containers, read through the Engine API
          --watch          print /events as they happen, until Ctrl+C
          --autostart      on | off | status  - off unless you turn it on

          --version        what this build calls itself
          --help           this

        Exit code 0 means the verb finished; 1 names what stopped it; 2 is an argument
        this executable does not have.

        """;
}
