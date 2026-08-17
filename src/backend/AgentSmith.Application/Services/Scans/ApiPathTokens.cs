namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: the path-like tokens in a citation, and whether one of them is the endpoint a
/// specification declares — <c>CitedFileIndex.PathsIn</c>'s rule, pointed at URLs.
/// <para>
/// A live-target citation is written for a HUMAN and for a scanner in the same breath:
/// "GET /orders/{id}", "https://host/api/orders/42 (unauthenticated)", "/orders/42?full=1".
/// The declaration is a template. Refusing a real endpoint over a method prefix, a host, a
/// query string or a concrete id turns evidence into invention, which is the failure p0422
/// paid for once already.
/// </para>
/// </summary>
public static class ApiPathTokens
{
    /// <summary>Every token in a citation that could name a path, most specific first.</summary>
    public static IEnumerable<string> PathsIn(string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation)) yield break;
        foreach (var token in citation.Split(
            [' ', '\t', '\n', '(', ')', ',', ';', '"', '\'', '`', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            var path = PathOf(token);
            if (path.Length > 0) yield return path;
        }
    }

    /// <summary>The segments of a path, with the scheme, host, query and fragment gone.</summary>
    public static string[] Segments(string? path) =>
        (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Does a cited path name this declared one? A <c>{param}</c> segment matches any
    /// single segment, and the citation may carry a base-path prefix the specification
    /// leaves out — so the declared segments must match the citation's TAIL.
    /// </summary>
    public static bool Matches(IReadOnlyList<string> declared, IReadOnlyList<string> cited)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(cited);
        if (declared.Count == 0 || cited.Count < declared.Count) return false;
        var offset = cited.Count - declared.Count;
        for (var i = 0; i < declared.Count; i++)
        {
            var d = declared[i];
            if (d.StartsWith('{') && d.EndsWith('}')) continue;
            if (!string.Equals(d, cited[offset + i], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static string PathOf(string token)
    {
        var text = token.Trim().Trim('.', ':');
        if (IsVerb(text)) return string.Empty;
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            var host = text.IndexOf('/', scheme + 3);
            text = host < 0 ? "/" : text[host..];
        }
        foreach (var cut in new[] { '?', '#' })
        {
            var at = text.IndexOf(cut);
            if (at >= 0) text = text[..at];
        }
        return text.Contains('/', StringComparison.Ordinal) ? text : string.Empty;
    }

    /// <summary>"GET /orders/{id}" names one endpoint, not a path called GET.</summary>
    private static bool IsVerb(string text) => text.ToUpperInvariant() is
        "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" or "TRACE";
}
