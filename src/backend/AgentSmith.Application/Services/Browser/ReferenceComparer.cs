namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: compares two sources in ONE run of the render script — both staged into the
/// same browser sandbox, both loaded in one browser at each viewport — and hands back the script's
/// report with the diff images. The sources are any render_reference takes.
/// </summary>
public sealed class ReferenceComparer(
    BrowserSandboxOpener opener, RenderSourceStager stager, BrowserRenderInvocation invocation)
{
    private const string WorkRoot = "/work";

    /// <summary>The comparison, or why there is none.</summary>
    public async Task<(BrowserRenderOutput? Output, string? Refusal)> CompareAsync(
        RenderReferenceScope scope, RenderSource reference, RenderSource candidate,
        IReadOnlyList<SelectorPair> pairs, IReadOnlyList<string> viewports, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var (lease, refusal) = await opener.OpenAsync(scope.Project, scope.Conversation, ct);
        if (lease is null) return (null, refusal);
        await using (lease)
        {
            var (left, whyLeft) = await stager.StageAsync(lease.Sandbox, scope, reference, ct);
            if (left is null) return (null, $"the reference could not be staged: {whyLeft}");
            var (right, whyRight) = await stager.StageAsync(lease.Sandbox, scope, candidate, ct);
            if (right is null) return (null, $"the candidate could not be staged: {whyRight}");
            var request = new BrowserRenderRequest(null, null, string.Empty, [], BrowserStyleProperties.Properties,
                $"{WorkRoot}/render/{Guid.NewGuid():N}",
                new BrowserCompareRequest(BrowserCompareSide.Of(left), BrowserCompareSide.Of(right), pairs, viewports));
            var (result, shots, failure) = await invocation.RunAsync(lease.Sandbox, request, ct);
            return result?.Compare is null
                ? (null, failure ?? result?.Error ?? "the render script reported no comparison")
                : (new BrowserRenderOutput(result, shots, [.. left.Notes, .. right.Notes]), null);
        }
    }
}
