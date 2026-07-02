namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: a parsed <c>connection/pattern</c> repo reference. A leading <c>!</c> marks an
/// exclude. A reference without a <c>/</c> is NOT a connection ref — it is a legacy repos:
/// catalog name and is resolved by the catalog path instead.
/// </summary>
public sealed record RepoGlobRef(string Connection, string Pattern, bool IsExclude)
{
    /// <summary>
    /// p0285: true when the pattern contains a wildcard, so the ref needs discovery+filtering.
    /// A wildcard-free include (e.g. <c>acme/Service.Api</c>) resolves STATICALLY instead.
    /// </summary>
    public bool IsGlob => Pattern.Contains('*');

    /// <summary>True when the raw entry references a connection (has a <c>/</c> separator).</summary>
    public static bool IsConnectionRef(string entry)
    {
        var body = entry.StartsWith('!') ? entry[1..] : entry;
        return body.Contains('/');
    }

    public static RepoGlobRef Parse(string entry)
    {
        var isExclude = entry.StartsWith('!');
        var body = isExclude ? entry[1..] : entry;
        var slash = body.IndexOf('/');
        return new RepoGlobRef(body[..slash], body[(slash + 1)..], isExclude);
    }
}
