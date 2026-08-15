using System.Text.Json;
using System.Text.Json.Nodes;

namespace FreeWilly.Core.Engine;

/// <summary>
/// Respells the Windows bind sources in a container-create payload the distribution's way (DD125).
/// </summary>
/// <remarks>
/// <b>The translation is DD75's, moved under the client.</b> That task taught <c>do compose up</c> to
/// respell a drive letter by writing an override file, which works and reaches exactly one caller.
/// Every other Docker client on the machine — a plain <c>docker compose up</c>, an <c>ant</c> or a
/// <c>make</c> that shells into one, testcontainers, an IDE plugin — still hands the daemon
/// <c>D:\project\volumes\data</c> and is refused, because compose resolves <c>./volumes/data</c>
/// against the project before anything sees it and the daemon is Linux.
///
/// <para><b>So the rule belongs on the wire, where every client already is.</b>
/// <see cref="EnginePipeRelay"/> is the one thing between a Windows Docker client and the daemon —
/// a Linux <c>dockerd</c> cannot own a Windows named pipe, so the relay exists whether or not this
/// does. Rewriting there is what makes the translation transparent: no file of the user's changes,
/// no verb has to be substituted, and a compose file stays portable to Docker Desktop, which does
/// the same job in its own daemon.</para>
///
/// <para><b>The spelling is <see cref="Wsl.WindowsFolderSpelledElsewhere"/> and is not restated
/// here.</b> One rule about where a Windows folder lives inside the distribution, already used by
/// <c>do compose up</c> and by <c>read doctor</c>. It carries the Docker Desktop spellings too, so a
/// compose file written against <c>/run/desktop/mnt/host/d/…</c> lands here as well.</para>
///
/// <para><b>Pure, and that is deliberate.</b> The relay is on every client's hot path and a defect
/// here corrupts somebody's container rather than failing a test. Bytes in, bytes out, no I/O — so
/// what the daemon would receive can be asserted directly.</para>
/// </remarks>
public static class ContainerCreateRewrite
{
    /// <summary>
    /// Respell the bind sources in a create payload, if it holds any this daemon cannot resolve.
    /// </summary>
    /// <param name="body">The request body, as the client sent it.</param>
    /// <param name="rewritten">The body to forward, set only where something changed.</param>
    /// <returns><see langword="true"/> where a source was respelled.</returns>
    /// <remarks>
    /// False rather than an exception for anything unparseable. A body this does not understand is a
    /// body the daemon is entitled to answer for itself: the relay forwards it untouched and the
    /// client gets the daemon's own error, which is a better sentence than one invented here.
    /// </remarks>
    public static bool TryRewrite(ReadOnlySpan<byte> body, out byte[] rewritten)
    {
        rewritten = [];

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body.ToArray());
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is not JsonObject payload
            || payload["HostConfig"] is not JsonObject hostConfig)
        {
            return false;
        }

        var changed = RewriteBinds(hostConfig["Binds"] as JsonArray);
        changed |= RewriteMounts(hostConfig["Mounts"] as JsonArray);

        if (!changed)
        {
            return false;
        }

        rewritten = JsonSerializer.SerializeToUtf8Bytes(payload);
        return true;
    }

    /// <summary>The short syntax: one string per mount, colon-separated.</summary>
    /// <remarks>
    /// What compose sends for <c>- ./data:/data</c>, and what the daemon reported back in the failure
    /// this was filed for: <c>invalid volume specification:
    /// 'D:\…\repository:/opt/aem/crx-quickstart/repository:rw'</c>.
    /// </remarks>
    private static bool RewriteBinds(JsonArray? binds)
    {
        if (binds is null)
        {
            return false;
        }

        var changed = false;
        for (var index = 0; index < binds.Count; index++)
        {
            if (binds[index]?.GetValue<string>() is not { } spec)
            {
                continue;
            }

            if (Respelled(spec) is not { } replacement)
            {
                continue;
            }

            binds[index] = replacement;
            changed = true;
        }

        return changed;
    }

    /// <summary>The long syntax: an object per mount, with the source in its own field.</summary>
    /// <remarks>
    /// Only <c>bind</c>. A volume's source is a name and a tmpfs has none, and respelling either
    /// would invent a path where the user asked for storage the daemon owns.
    /// </remarks>
    private static bool RewriteMounts(JsonArray? mounts)
    {
        if (mounts is null)
        {
            return false;
        }

        var changed = false;
        foreach (var entry in mounts)
        {
            if (entry is not JsonObject mount
                || mount["Type"]?.GetValue<string>() is not "bind"
                || mount["Source"]?.GetValue<string>() is not { } source)
            {
                continue;
            }

            if (Wsl.WindowsFolderSpelledElsewhere(source) is not { } inside)
            {
                continue;
            }

            mount["Source"] = inside;
            changed = true;
        }

        return changed;
    }

    /// <summary>One short-syntax spec, respelled, or <see langword="null"/> to leave it alone.</summary>
    /// <remarks>
    /// <b>The drive letter's own colon is the whole difficulty.</b> <c>D:\a:/b:rw</c> splits into
    /// three fields on a naive scan and the daemon says <c>invalid mode: /b</c> — the failure DD75
    /// measured. So the source ends at the first colon <i>after</i> the drive letter's, and at the
    /// first colon of all where there is no drive letter.
    ///
    /// <para>A spec with no colon at all is an anonymous volume — a target and nothing else — and a
    /// source that translates to nothing is a named volume. Both are returned unchanged, which is why
    /// this answers null rather than a string: "no change" and "changed to the same thing" are
    /// different answers and only one of them should mark the payload dirty.</para>
    /// </remarks>
    /// <param name="spec">A bind spec, e.g. <c>D:\project\data:/data:rw</c>.</param>
    /// <returns>The respelled spec, or null.</returns>
    internal static string? Respelled(string spec)
    {
        if (string.IsNullOrEmpty(spec))
        {
            return null;
        }

        var end = spec.IndexOf(':', Wsl.HasDriveLetter(spec) ? 2 : 0);
        if (end < 0)
        {
            return null;
        }

        var source = spec[..end];

        return Wsl.WindowsFolderSpelledElsewhere(source) is { } inside
            ? inside + spec[end..]
            : null;
    }
}
