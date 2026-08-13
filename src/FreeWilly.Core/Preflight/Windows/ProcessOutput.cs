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
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Run <paramref name="fileName"/> with <paramref name="arguments"/> and return what it wrote.
    /// </summary>
    /// <param name="fileName">The executable, as a full path.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The exit code and output, or the reason there is neither.</returns>
    internal static ProcessOutput Run(string fileName, params string[] arguments)
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

            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                TryKill(process);
                return new ProcessOutput(
                    null, "", $"{fileName} did not finish within {Timeout.TotalSeconds:0} seconds");
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
