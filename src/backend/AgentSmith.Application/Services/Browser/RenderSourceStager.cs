using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283dh: makes one source ready in the browser sandbox. An uploaded set is copied from
/// the store into <c>/work/sets/&lt;setId&gt;/</c> once per sandbox life; a repository page is copied
/// out of its repository's sandbox; a URL needs nothing. One renderer for three sources: they differ
/// only in where the bytes come from. 2026-10-13-8762r: a page given inline is written under
/// <c>/work/mocks/&lt;setId&gt;/</c>.
/// </summary>
public sealed class RenderSourceStager(ReferenceSetMaterialiser sets, RepoRenderSource repos)
{
    internal const string SetsRoot = "sets";
    internal const string MocksRoot = "mocks";
    private const string WorkRoot = "/work";

    /// <summary>The staged source, or why it could not be staged.</summary>
    public async Task<(StagedSource? Staged, string? Refusal)> StageAsync(
        ISandbox browser, RenderReferenceScope scope, RenderSource source, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Url is { } url) return (new StagedSource(url.AbsoluteUri, null, string.Empty, []), null);
        if (source is { Inline: { } inline, SetId: { } mock })
        {
            await sets.WriteUnderAsync(browser, inline, $"{MocksRoot}/{mock}", ct);
            return (new StagedSource(null, $"{WorkRoot}/{MocksRoot}/{mock}", source.Page, []), null);
        }
        if (source.SetId is { } setId)
        {
            var root = $"{SetsRoot}/{setId}";
            await sets.PrepareUnderAsync(browser, source.Session, setId, root, ct);
            return (new StagedSource(null, $"{WorkRoot}/{root}", source.Page, []), null);
        }
        return await repos.CopyAsync(browser, scope.RepoSandboxes, source.Repo, source.RepoPath!, ct);
    }
}
