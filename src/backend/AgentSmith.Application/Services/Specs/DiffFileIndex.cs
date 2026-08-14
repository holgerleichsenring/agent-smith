namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the set of paths a unified diff actually touches, and whether a claimed
/// citation lands in it.
/// <para>
/// This is the whole mechanical half of the accounting. We cannot check that a hunk
/// MEANS what the account says; we can check that the file it names was changed at all,
/// which is what separates a citation from a fabrication.
/// </para>
/// </summary>
public sealed class DiffFileIndex
{
    private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

    public DiffFileIndex(string diff)
    {
        foreach (var line in (diff ?? string.Empty).Split('\n'))
        {
            if (!line.StartsWith("+++ ", StringComparison.Ordinal)
                && !line.StartsWith("--- ", StringComparison.Ordinal)) continue;
            var path = line[4..].Trim();
            if (path is "/dev/null" or "") continue;
            // git prefixes the two sides with a/ and b/.
            if (path.Length > 2 && (path.StartsWith("a/", StringComparison.Ordinal)
                                 || path.StartsWith("b/", StringComparison.Ordinal)))
                path = path[2..];
            paths.Add(Normalize(path));
        }
    }

    public IReadOnlyCollection<string> Paths => paths;

    public bool IsEmpty => paths.Count == 0;

    /// <summary>
    /// Does this citation name something the diff touched? Suffix matching, because an
    /// account may cite "SwaggerExtensions.cs" for "src/Api/Extensions/SwaggerExtensions.cs"
    /// and a reviewer reads both the same way. A citation naming a line ("File.cs:44")
    /// or a hunk keeps only its path part.
    /// </summary>
    public bool Contains(string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation)) return false;
        var needle = Normalize(citation.Split(':', 2)[0].Trim());
        if (needle.Length == 0) return false;
        return paths.Any(p =>
            p.Equals(needle, StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/" + needle, StringComparison.OrdinalIgnoreCase)
            || needle.EndsWith("/" + p, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').Trim();
}
