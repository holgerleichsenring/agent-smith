using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283di: compare_reference — a reference and a candidate rendered in one browser, every
/// computed-style difference of the selector pairs the model maps listed by name with both values,
/// and a screenshot similarity with a diff image. The model chooses the pairs; the code drives.
/// Neither level gates anything: both report, and a run's outcome is unchanged by them.
/// </summary>
public sealed class CompareReferenceToolHost(CompareReferenceServices services, RenderReferenceScope scope) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(CompareReference, name: "compare_reference")];

    [Description("Compares a candidate page with a reference page in one real browser. Lists every computed-style property that differs between each reference selector and the candidate selector you map it to, with both values, and a selector that matches nothing; adds the share of differing screenshot pixels (the shorter page padded, so a missing section counts) and shows a diff image. It reports; it decides nothing.")]
    public async Task<string> CompareReference(
        [Description("The reference: reference:<name>[/<page>], [<repo>/]<path>.html or an absolute http(s) URL.")] string reference,
        [Description("The candidate, in the same forms — typically the page you built.")] string candidate,
        [Description("Selector pairs, at most 30: 'reference selector => candidate selector', or one selector for both sides.")] string[] pairs,
        [Description("desktop (default, 1440x900), mobile (390x844) or both.")] string? viewport = null,
        CancellationToken ct = default)
    {
        var (mapped, badPairs) = ComparePairs.Parse(pairs);
        if (mapped is null) return $"Error: {badPairs}";
        if (ComparePairs.Viewports(viewport) is not { } viewports) return "Error: viewport is desktop, mobile or both.";
        var (left, whyLeft) = await SourceAsync(reference, ct);
        if (left is null) return $"Error: reference — {whyLeft}";
        var (right, whyRight) = await SourceAsync(candidate, ct);
        if (right is null) return $"Error: candidate — {whyRight}";
        await scope.Serial.WaitAsync(ct);
        try
        {
            var (output, failure) = await services.Comparer.CompareAsync(scope, left, right, mapped, viewports, ct);
            return output is null ? $"compare_reference failed: {failure}" : await ReportAsync(reference, candidate, mapped, output, ct);
        }
        finally
        {
            scope.Serial.Release();
        }
    }

    private async Task<(RenderSource?, string?)> SourceAsync(string text, CancellationToken ct)
    {
        var (source, refusal) = services.Render.Sources.Parse(text, scope);
        if (source?.Url is { } url && await services.Render.Guard.RefusalForAsync(url, ct) is { } refused)
            return (null, $"refused: {refused}. Only public addresses are rendered.");
        return (source, refusal);
    }

    private async Task<string> ReportAsync(
        string reference, string candidate, IReadOnlyList<SelectorPair> pairs, BrowserRenderOutput output, CancellationToken ct)
    {
        var report = output.Result.Compare!;
        var comparison = new VisualComparison(reference, candidate, [.. report.Viewports.Select(v => new ViewportComparison(
            v.Viewport, v.MismatchRatio, v.ReferenceHeight, v.CandidateHeight, v.PaddedHeight,
            services.Differences.Compare(pairs, v.Reference, v.Candidate, BrowserStyleProperties.Properties)))], []);
        var diffs = output.Result.Shots.Zip(output.Shots, (shot, jpeg) => (shot.Viewport, jpeg)).ToList();
        if (scope.Run is { } run)
            comparison = await services.Recorder.RecordAsync(run, scope.RepoSandboxes.Values.FirstOrDefault(), comparison, diffs, ct);
        var notes = diffs.Select(d => Deposit(reference, candidate, d.Viewport, d.jpeg)).Concat(output.Notes ?? []).ToList();
        return CompareResultText.Render(output.Result, comparison, notes);
    }

    private string Deposit(string reference, string candidate, string viewport, byte[] jpeg)
    {
        var caption = $"{viewport} diff of {candidate} against {reference} (changed pixels marked; magenta is padding)";
        var deposited = services.Render.Images.Deposit(new ToolImage("image/jpeg", jpeg, caption));
        return deposited.IsAccepted ? $"{caption}: shown" : $"{caption}: not shown — {deposited.Refusal}";
    }
}
