using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: the path one hunk of a unified diff is about.
/// <para>
/// Read off the <c>+++ b/…</c> header rather than the <c>diff --git</c> line, because git
/// quotes a path carrying a space or a non-ASCII byte (<c>core.quotePath</c>) and the one-line
/// form is then unparseable: the file would vanish from the prompt AND from the path list, so a
/// true finding on it could never be admitted. The path the file ENDS UP at, because a rename's
/// a-side names a path the reviewer cannot open — except for a deletion, whose <c>+++</c> is
/// <c>/dev/null</c> and whose a-side is the only name it has.
/// </para>
/// </summary>
internal static class PhaseDiffPath
{
    public static string? Of(string chunk)
    {
        foreach (var (marker, prefix) in new[] { ("+++ ", "b/"), ("--- ", "a/") })
        {
            if (Header(chunk, marker) is not { } raw || raw == "/dev/null") continue;
            var path = Unquote(raw);
            if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[2..];
            if (path.Length > 0) return path;
        }
        // No content headers at all: a pure rename or a mode change. The one-line form is all
        // there is, and a quoted one yields nothing rather than a path that does not exist.
        var line = chunk.Split('\n', 2)[0];
        var b = line.LastIndexOf(" b/", StringComparison.Ordinal);
        return b < 0 || line.Contains('"', StringComparison.Ordinal)
            ? null
            : line[(b + 3)..].Trim() is { Length: > 0 } fallback ? fallback : null;
    }

    private static string? Header(string chunk, string marker)
    {
        foreach (var line in chunk.Split('\n'))
        {
            if (!line.StartsWith(marker, StringComparison.Ordinal)) continue;
            // "+++ b/path\t2026-09-17" — some formats append a timestamp after a tab.
            var rest = line[marker.Length..];
            var tab = rest.IndexOf('\t', StringComparison.Ordinal);
            return (tab < 0 ? rest : rest[..tab]).TrimEnd('\r').Trim();
        }
        return null;
    }

    /// <summary>
    /// A C-quoted path as git writes it: the quotes off, the escapes resolved. Non-ASCII is
    /// written as octal BYTES of the UTF-8 encoding, so the bytes are collected and decoded
    /// once rather than turned into characters one at a time.
    /// </summary>
    public static string Unquote(string path)
    {
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"') return path;
        var inner = path[1..^1];
        var bytes = new List<byte>(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] != '\\' || i + 1 >= inner.Length)
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(inner[i].ToString()));
                continue;
            }
            var next = inner[++i];
            if (next is >= '0' and <= '7' && i + 2 < inner.Length)
            {
                bytes.Add(Convert.ToByte(inner.Substring(i, 3), 8));
                i += 2;
                continue;
            }
            bytes.Add((byte)(next switch { 't' => '\t', 'n' => '\n', 'r' => '\r', _ => next }));
        }
        return Encoding.UTF8.GetString([.. bytes]);
    }
}
