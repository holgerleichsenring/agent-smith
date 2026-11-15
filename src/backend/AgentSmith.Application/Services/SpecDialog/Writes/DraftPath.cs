namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: how a draft is laid out as files under <c>draft:</c>. A spec is <c>specs/&lt;id&gt;.yaml</c> and an
/// epic's parent <c>epic/&lt;id&gt;.yaml</c>, the file name being the spec's id; a ticket is
/// <c>tickets/&lt;key&gt;.yaml</c>; the bug, the comment and an epic's series contract have one file each.
/// </summary>
public static class DraftPath
{
    public const string Prefix = "draft:";
    public const string Specs = "specs/";
    public const string Epic = "epic/";
    public const string Tickets = "tickets/";
    public const string Bug = "bug.yaml";
    public const string Comment = "comment.md";
    public const string Contract = "contract.yaml";
    private const string Yaml = ".yaml";

    /// <summary>The layout, for an answer that names a path outside it.</summary>
    public const string Layout =
        "draft:specs/<id>.yaml, draft:epic/<id>.yaml, draft:contract.yaml, draft:tickets/<key>.yaml, draft:bug.yaml, draft:comment.md";

    /// <summary>Whether a tool's path addresses the draft.</summary>
    public static bool Names(string? path) => (path ?? string.Empty).TrimStart().StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The path inside the draft: no prefix, no leading slash or "./".</summary>
    public static string Bare(string? path)
    {
        var bare = (path ?? string.Empty).Trim();
        if (bare.StartsWith(Prefix, StringComparison.Ordinal)) bare = bare[Prefix.Length..];
        while (bare.StartsWith('/') || bare.StartsWith("./", StringComparison.Ordinal))
            bare = bare[(bare[0] == '/' ? 1 : 2)..];
        return bare;
    }

    public static string Shown(string bare) => Prefix + bare;

    public static string OfSpec(string id, bool parent) => (parent ? Epic : Specs) + id + Yaml;

    public static string OfTicket(string key) => Tickets + key + Yaml;

    /// <summary>The item a bare path names, or null when it lies outside the layout.</summary>
    public static DraftItem? Item(string bare) => bare switch
    {
        Bug => new(DraftItemKind.Bug, Bug),
        Comment => new(DraftItemKind.Comment, Comment),
        Contract => new(DraftItemKind.Contract, Contract),
        _ => Named(bare, Specs, DraftItemKind.Spec) ?? Named(bare, Epic, DraftItemKind.Epic)
            ?? Named(bare, Tickets, DraftItemKind.Ticket),
    };

    /// <summary>A directory of the draft, as a delete names it: the whole draft, specs/, epic/ or tickets/.</summary>
    public static bool IsDirectory(string bare) =>
        bare.TrimEnd('/') is "" or "specs" or "epic" or "tickets";

    private static DraftItem? Named(string bare, string directory, DraftItemKind kind)
    {
        if (!bare.StartsWith(directory, StringComparison.Ordinal) || !bare.EndsWith(Yaml, StringComparison.Ordinal)) return null;
        var name = bare[directory.Length..^Yaml.Length];
        return name.Length == 0 || name.Contains('/') ? null : new DraftItem(kind, name);
    }
}
