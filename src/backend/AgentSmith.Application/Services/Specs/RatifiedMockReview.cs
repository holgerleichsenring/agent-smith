using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The diff review's offer to compare the page a spec built with the mock a person ratified for it. The
/// mock is the one the set's spec names (<c>ratified_mock</c>) among the mocks read beside it; a CANDIDATE
/// is every static .html this spec's diff changed outside <c>.agentsmith/</c>, where a mock or a companion
/// is never one. With both and the run's browser, the reviewer gets the render and compare tools, three
/// more iterations and one more look, so the read a compare finding must cite is never refused.
/// <para>A compare is a finding, never a gate; one not made is said, never counted as made.</para>
/// </summary>
public sealed class RatifiedMockReview(RenderReferenceToolFactory references)
{
    public const string CompareTool = "compare_reference";

    public RatifiedMockOffer Offer(PipelineContext pipeline, SpecDraft draft, IReadOnlyList<SpecDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(diffs);
        var spec = pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set)
            ? set?.Specs.FirstOrDefault(s => s.SpecId == draft.SpecId) : null;
        if (spec?.Draft.RatifiedMock is not { } version) return RatifiedMockOffer.None;
        var said = $"Ratified mock v{version}";
        if (RatifiedMockFiles.Beside(spec) is not { } mock)
            return RatifiedMockOffer.NotCompared($"{said} not compared: it is not beside the spec.");
        var candidates = Candidates(pipeline, diffs);
        if (candidates.Count == 0)
            return RatifiedMockOffer.NotCompared($"{said} not compared: the spec built no static page.");
        var tools = references.Tools(pipeline, false);
        if (!tools.Any(t => t.Name == CompareTool))
            return RatifiedMockOffer.NotCompared($"{said} not compared: the run has no browser.");
        return new RatifiedMockOffer(version, mock, candidates, tools, null, VisualComparisonRecorder.Recorded(pipeline).Count);
    }

    // Repository-qualified as compare_reference reads a repository page: <repo>/<path>.html.
    private static List<string> Candidates(PipelineContext pipeline, IReadOnlyList<SpecDiff> diffs)
    {
        var names = SpecReviewRepositories.Names(pipeline);
        return [.. diffs.SelectMany(d => d.Paths
            .Where(p => p.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                && !p.StartsWith(".agentsmith/", StringComparison.Ordinal))
            .Select(p => $"{names.GetValueOrDefault(d.SandboxKey) ?? d.SandboxKey}/{p}"))];
    }
}

/// <summary>What the diff review offers for the ratified mock: tools, terms and prompt section, or why not.</summary>
public sealed record RatifiedMockOffer(
    int? Version, string? Mock, IReadOnlyList<string> Candidates, IReadOnlyList<AITool> Tools, string? NotComparedNote,
    int RecordedBefore)
{
    /// <summary>The reviewer's extra iterations when the compare is offered: render, compare, and the cited read.</summary>
    public const int ExtraIterations = 3;

    public static RatifiedMockOffer None { get; } = new(null, null, [], [], null, 0);

    public static RatifiedMockOffer NotCompared(string note) => new(null, null, [], [], note, 0);

    public bool IsOffered => Tools.Count > 0;

    /// <summary>The spec review's look terms, with one more look when the compare is offered.</summary>
    public DerivationLookTerms Terms => IsOffered
        ? DerivationLookTerms.SpecReview with { Allowance = DerivationLookTerms.SpecReviewAllowance + 1 }
        : DerivationLookTerms.SpecReview;

    /// <summary>The prompt section asking for the compare; empty when it is not offered.</summary>
    public string PromptSection => !IsOffered ? string.Empty : $"""

        ## The ratified design mock
        A person ratified `{Mock}` (version {Version}) as what this spec's page must look like. Compare the
        page this spec built with it: render_reference shows a page, compare_reference compares the mock (the
        reference, by its repository path) with the built page (the candidate). The candidates in this diff:
        {string.Join("\n", Candidates.Select(c => $"- `{c}`"))}
        Compare the one that renders the ratified page. A difference the done-list does not excuse is a finding,
        and it must cite YOUR READ of the built file's line that renders it, or it is not admitted.

        """;

    /// <summary>The sentence the step result carries after the review; empty when no mock was ratified.</summary>
    public string After(PipelineContext pipeline)
    {
        if (NotComparedNote is { } note) return " " + note;
        if (!IsOffered) return string.Empty;
        return VisualComparisonRecorder.Recorded(pipeline).Count > RecordedBefore
            ? $" Ratified mock v{Version} compared with the built page."
            : $" Ratified mock v{Version} offered, not compared.";
    }
}
