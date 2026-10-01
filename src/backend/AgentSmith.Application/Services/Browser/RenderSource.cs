namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: what render_reference was asked to render — an uploaded set (its id and the
/// page inside it, empty for the set's own entry page) or a public URL. Exactly one is set.
/// </summary>
public sealed record RenderSource(string? SetId, string Page, Uri? Url)
{
    public static RenderSource OfSet(string setId, string page) => new(setId, page, null);

    public static RenderSource OfUrl(Uri url) => new(null, string.Empty, url);
}
