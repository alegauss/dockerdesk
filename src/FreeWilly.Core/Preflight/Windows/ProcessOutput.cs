using System.Diagnostics;
using System.Text;

namespace FreeWilly.Core.Preflight.Windows;

/// <summary>What running a console tool produced.</summary>
/// <param name="ExitCode">The process exit code, or <see langword="null"/> if it never ran.</param>
/// <param name="Output">Standard output and standard error, decoded and concatenated.</param>
/// <param name="Failure">Why it never ran or never finished, when that is what happened.</param>
internal sealed record ProcessOutput(int? ExitCode, string Output, string? Failure)
{
    /// <summary>Whether the tool ran to completion and said it succeeded.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs a console tool and decodes what it wrote, whichever encoding it chose.</summary>
internal static class ConsoleTool
{
    /// <summary>How long a preflight probe waits for a tool before giving up on it.</summary>
    /// <remarks>
    /// Short on purpose, and DD122 is why it stayed short. A probe asks a question — <c>wsl
    /// --status</c>, <c>--list</c> — and a machine that has not answered one in fifteen seconds is
    /// not slow, it is stuck; a preflight that waits minutes to say so has stopped being a
    /// preflight. What DD122 changed is that this is no longer the only budget there is.
    /// </remarks>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Run a tool under the probe budget.</summary>
    /// <param name="fileName">The executable, as a full path.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The exit code and output, or the reason there is neither.</returns>
    internal static ProcessOutput Run(string fileName, params string[] arguments) =>
        Run(Timeout, fileName, arguments);

    /// <summary>
    /// Run <paramref name="fileName"/> with <paramref name="arguments"/> and return what it wrote.
    /// </summary>
    /// <param name="budget">How long it may take before it is killed and reported as unfinished.</param>
    /// <param name="fileName">The executable, as a full path.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The exit code and output, or the reason there is neither.</returns>
    /// <remarks>
    /// The budget is a parameter since DD122, because one number cannot serve both callers. It was
    /// <see cref="Timeout"/> for everything, and the provision inherited a budget written for a
    /// question: measured on a clean Windows 11 machine, every artefact downloaded and verified, the
    /// distribution imported, and the step that unpacks the engine inside it killed at fifteen
    /// seconds — because a distribution that has never run boots cold and then untars 85 MB.
    /// </remarks>
    internal static ProcessOutput Run(TimeSpan budget, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessOutput(null, "", $"{fileName} could not be started");
            }

            // Read the raw bytes rather than letting a StreamReader guess: wsl.exe writes UTF-16LE,
            // and decoding that as UTF-8 yields a string with a NUL after every character — which
            // matches no pattern and silently reads as "WSL is not installed".
            using var stdout = new MemoryStream();
            var copyOut = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            using var stderr = new MemoryStream();
            var copyErr = process.StandardError.BaseStream.CopyToAsync(stderr);

            if (!process.WaitForExit((int)budget.TotalMilliseconds))
            {
                TryKill(process);

                // The budget is in the sentence, so a log tells a slow machine from a stuck one.
                // With one constant it could not: "did not finish within 15 seconds" read the same
                // whether it was a question nothing answered or an unpack that needed a minute.
                return new ProcessOutput(
                    null, "", $"{fileName} did not finish within {Spell(budget)}");
            }

            Task.WaitAll(copyOut, copyErr);
            var text = Decode(stdout.ToArray()) + Decode(stderr.ToArray());
            return new ProcessOutput(process.ExitCode, text, null);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new ProcessOutput(null, "", $"{fileName}: {exception.Message}");
        }
    }

    /// <summary>How a budget is written into the sentence that says it was exceeded.</summary>
    /// <param name="budget">The budget.</param>
    /// <returns>Seconds under a minute, minutes at or above one.</returns>
    /// <remarks>
    /// "300 seconds" is a number a reader converts before it means anything, and the two budgets
    /// this now has sit on either side of the unit that reads naturally for each.
    /// </remarks>
    internal static string Spell(TimeSpan budget) =>
        budget < TimeSpan.FromMinutes(1)
            ? $"{budget.TotalSeconds:0} seconds"
            : $"{budget.TotalMinutes:0} minutes";

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // It exited between the wait and the kill. Nothing to do, and nothing worth saying.
        }
    }

    /// <summary>
    /// Decode console bytes as UTF-16LE or UTF-8, deciding by what is there rather than by what
    /// the tool documents.
    /// </summary>
    /// <param name="bytes">The raw bytes.</param>
    /// <returns>The decoded text.</returns>
    internal static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            return "";
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // No BOM: UTF-16LE ASCII text puts a zero in every odd byte, which valid UTF-8 never does.
        var pairs = bytes.Length / 2;
        if (pairs > 0)
        {
            var zeroes = 0;
            for (var i = 1; i < bytes.Length; i += 2)
            {
                if (bytes[i] == 0)
                {
                    zeroes++;
                }
            }

            if (zeroes * 2 > pairs)
            {
                return Encoding.Unicode.GetString(bytes);
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
