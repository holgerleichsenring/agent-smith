namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: what render_reference was asked to render — an uploaded set (its id and the
/// page inside it, empty for the set's own entry page) or a public URL. Exactly one is set.
/// 2026-10-01-283df: a set also names the conversation it was uploaded to, where the store keeps it
/// — in a run that is the approval's conversation, not a conversation the run has.
/// </summary>
public sealed record RenderSource(string? SetId, string Page, Uri? Url, string Session = "")
{
    public static RenderSource OfSet(string setId, string page, string session) => new(setId, page, null, session);

    public static RenderSource OfUrl(Uri url) => new(null, string.Empty, url);
}
