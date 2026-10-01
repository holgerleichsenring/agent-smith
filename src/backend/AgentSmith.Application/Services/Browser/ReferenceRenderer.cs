using AgentSmith.Application.Services.Sandbox;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: renders one source in the conversation's browser sandbox. An uploaded set is
/// copied from the store into <c>/work/sets/&lt;setId&gt;/</c> once per sandbox life — the
/// materialiser's marker says it is there — and served by the script from a local origin; a URL is
/// handed over as it is, already judged by <see cref="RenderUrlGuard"/>.
/// </summary>
public sealed class ReferenceRenderer(
    BrowserSandboxOpener opener, ReferenceSetMaterialiser sets, BrowserRenderInvocation invocation)
{
    internal const string SetsRoot = "sets";
    private const string WorkRoot = "/work";

    /// <summary>The render, or why there is none.</summary>
    public async Task<(BrowserRenderOutput? Output, string? Refusal)> RenderAsync(
        RenderReferenceScope scope, RenderSource source, IReadOnlyList<string> selectors, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(source);
        var (lease, refusal) = await opener.OpenAsync(scope.Project, scope.Conversation, ct);
        if (lease is null) return (null, refusal);
        await using (lease)
        {
            string? siteDir = null;
            if (source.SetId is { } setId)
            {
                var root = $"{SetsRoot}/{setId}";
                await sets.PrepareUnderAsync(lease.Sandbox, source.Session, setId, root, ct);
                siteDir = $"{WorkRoot}/{root}";
            }
            var request = new BrowserRenderRequest(source.Url?.AbsoluteUri, siteDir, source.Page,
                selectors, BrowserStyleProperties.Properties, $"{WorkRoot}/render/{Guid.NewGuid():N}");
            var (result, shots, failure) = await invocation.RunAsync(lease.Sandbox, request, ct);
            return result is null ? (null, failure) : (new BrowserRenderOutput(result, shots), null);
        }
    }
}
