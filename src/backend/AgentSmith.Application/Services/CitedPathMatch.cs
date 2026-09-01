namespace AgentSmith.Application.Services;

/// <summary>
/// p0333/2026-09-01-85b2: whether a path somebody WROTE names a file the run really has.
/// <para>
/// The two sides never agree on the prefix. A master reads through its tools and cites
/// what its tools showed it — a context or workdir prefix included, 'default/x/y.cs' — while
/// a scanner reports 'x/y.cs' relative to the checkout root. Suffix matching on a segment
/// boundary absorbs exactly that mismatch without matching 'a.cs' to 'ba.cs'.
/// </para>
/// <para>
/// One rule, because the merge that suppresses a reviewed pattern and the reader that
/// fetches a finding's evidence are answering the same question about the same two strings.
/// A second copy of it would let them disagree about which file was cited.
/// </para>
/// </summary>
public static class CitedPathMatch
{
    public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');

    /// <summary>Do these two written paths name the same file?</summary>
    public static bool Same(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var l = Normalize(left);
        var r = Normalize(right);
        return l == r
            || l.EndsWith("/" + r, StringComparison.Ordinal)
            || r.EndsWith("/" + l, StringComparison.Ordinal);
    }

    /// <summary>Does any of these already-normalised paths name the same file?</summary>
    public static bool AnyNames(IEnumerable<string> normalized, string path)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        return normalized.Any(candidate => Same(candidate, path));
    }

    /// <summary>
    /// The forms worth ASKING a file store for, most likely first: the path as written and,
    /// when it carries a leading segment, the path without it. A store answers an exact key,
    /// so the prefix mismatch <see cref="Same"/> forgives has to be offered as a second key.
    /// </summary>
    public static IEnumerable<string> Forms(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = Normalize(path);
        yield return normalized;
        var firstSlash = normalized.IndexOf('/', StringComparison.Ordinal);
        if (firstSlash > 0 && firstSlash < normalized.Length - 1)
            yield return normalized[(firstSlash + 1)..];
    }
}
