namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: renders one source in the conversation's browser sandbox. An uploaded set is
/// copied from the store into <c>/work/sets/&lt;setId&gt;/</c> once per sandbox life — the
/// materialiser's marker says it is there — and served by the script from a local origin; a URL is
/// handed over as it is, already judged by <see cref="RenderUrlGuard"/>. 2026-10-01-283dh: staging
/// is <see cref="RenderSourceStager"/>'s, which also copies a page out of a repository.
/// </summary>
public sealed class ReferenceRenderer(
    BrowserSandboxOpener opener, RenderSourceStager stager, BrowserRenderInvocation invocation)
{
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
            var (staged, unstaged) = await stager.StageAsync(lease.Sandbox, scope, source, ct);
            if (staged is null) return (null, unstaged);
            var request = new BrowserRenderRequest(staged.Url, staged.SiteDir, staged.Page,
                selectors, BrowserStyleProperties.Properties, $"{WorkRoot}/render/{Guid.NewGuid():N}");
            var (result, shots, failure) = await invocation.RunAsync(lease.Sandbox, request, ct);
            return result is null ? (null, failure) : (new BrowserRenderOutput(result, shots, staged.Notes), null);
        }
    }
}
