using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DockerDesk.Core.Preflight;
using DockerDesk.Core.Preflight.Windows;

namespace DockerDesk.Preflight;

/// <summary>
/// The standalone form of the check. The install runs the same <see cref="PreflightInspection"/>;
/// this is what a user runs when a working setup stopped working and the question is what changed.
/// </summary>
internal static class Program
{
    /// <summary>Exit code when every blocking row is green.</summary>
    private const int Ready = 0;

    /// <summary>Exit code when something blocks an install — the whole point of an exit code here.</summary>
    private const int Blocked = 1;

    /// <summary>Exit code for an argument this program does not have.</summary>
    private const int Usage = 2;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static int Main(string[] args)
    {
        var json = false;
        foreach (var argument in args)
        {
            switch (argument)
            {
                case "--json":
                    json = true;
                    break;
                case "-h" or "--help":
                    Console.Out.Write(HelpText);
                    return Ready;
                default:
                    Console.Error.WriteLine($"dockerdesk-preflight: unknown argument {argument}");
                    Console.Error.Write(HelpText);
                    return Usage;
            }
        }

        // The report carries em dashes and arrows whatever the console's code page is.
        Console.OutputEncoding = Encoding.UTF8;

        var report = PreflightInspection.Run(new WindowsMachineFacts());

        Console.Out.Write(json
            ? JsonSerializer.Serialize(report, Json) + Environment.NewLine
            : ReportText.Render(report));

        return report.CanHostEngine ? Ready : Blocked;
    }

    private static string HelpText =>
        """
        dockerdesk-preflight — read what this machine can host, and change nothing.

          --json    the same report as JSON, for an installer rather than a person
          --help    this

        Exit code 0 means every blocking row is green; 1 means at least one is not.

        """;
}
