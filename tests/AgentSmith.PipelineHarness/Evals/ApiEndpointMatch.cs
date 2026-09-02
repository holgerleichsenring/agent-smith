namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: whether a finding's location names a declared endpoint.
/// <para>
/// Scoring is by METHOD and PATH TEMPLATE, not by file: an api finding's location is an
/// endpoint and many carry no file at all. Matching on the template also survives the
/// difference between a finding that names the concrete request it made
/// (<c>GET /members/42</c>) and one that names the route (<c>/members/{id}</c>), which is
/// a wording difference and not a detection difference.
/// </para>
/// <para>
/// The METHOD is compared only when the finding names one. A finding that says
/// <c>/invoices</c> and nothing else has still named the only endpoint on that path;
/// requiring a verb it was never asked to state would score wording.
/// </para>
/// </summary>
public static class ApiEndpointMatch
{
    private static readonly string[] Methods =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public static bool Matches(ApiEndpointDeclaration declaration, string? location)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (string.IsNullOrWhiteSpace(location)) return false;
        var (method, path) = Split(location);
        if (method is not null
            && !string.Equals(method, declaration.Method, StringComparison.OrdinalIgnoreCase))
            return false;
        return SamePath(declaration.Path, path);
    }

    /// <summary>The method and the path a finding's location names, with the query string,
    /// any host prefix and any trailing punctuation removed. Method is null when the
    /// location does not name one.</summary>
    public static (string? Method, string Path) Split(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var text = location.Trim();
        string? method = null;
        foreach (var candidate in Methods)
        {
            if (!text.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase)) continue;
            method = candidate;
            text = text[(candidate.Length + 1)..].TrimStart();
            break;
        }
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            var slash = text.IndexOf('/', scheme + 3);
            text = slash >= 0 ? text[slash..] : "/";
        }
        var query = text.IndexOfAny(['?', '#']);
        if (query >= 0) text = text[..query];
        return (method, text.TrimEnd('.', ',', ';', ':', ')').Trim());
    }

    /// <summary>Segment-by-segment, with a <c>{placeholder}</c> matching any one concrete
    /// segment. Segment counts must agree, so <c>/orders</c> never matches
    /// <c>/orders/{id}</c> — they are two declarations with two verdicts.</summary>
    public static bool SamePath(string template, string candidate)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(candidate);
        var expected = Segments(template);
        var actual = Segments(candidate);
        if (expected.Length == 0 || expected.Length != actual.Length) return false;
        for (var i = 0; i < expected.Length; i++)
        {
            if (IsPlaceholder(expected[i]) || IsPlaceholder(actual[i])) continue;
            if (!string.Equals(expected[i], actual[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool IsPlaceholder(string segment) =>
        (segment.StartsWith('{') && segment.EndsWith('}'))
        || (segment.StartsWith(':') && segment.Length > 1);

    private static string[] Segments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
