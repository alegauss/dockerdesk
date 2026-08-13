using System.Reflection;

namespace DockerDesk.Core.Licensing;

/// <summary>What this build calls itself.</summary>
/// <remarks>
/// Read off the assembly rather than stated in two places. The informational version carries the
/// suffix a CI build stamps on; the plain one is what a local build has, and both are better than
/// a constant somebody has to remember to bump.
/// </remarks>
public static class BuildVersion
{
    /// <summary>This build's version, as a person would quote it in a bug report.</summary>
    public static string Current
    {
        get
        {
            var assembly = typeof(BuildVersion).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            // The SDK appends "+<commit>" to the informational version. The hash is not what a
            // person reads out of an About box.
            var plus = informational?.IndexOf('+', StringComparison.Ordinal) ?? -1;
            if (informational is not null)
            {
                return plus > 0 ? informational[..plus] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
