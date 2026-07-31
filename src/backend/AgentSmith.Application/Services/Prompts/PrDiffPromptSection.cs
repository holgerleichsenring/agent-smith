using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0312c: renders the pull request under review as a master prompt section.
/// Before this the diff reached the model only through PrReviewSkillPromptStrategy,
/// which died with the SkillRound machinery — a pr-review master without this
/// section would review nothing at all.
///
/// Empty string on pipelines that carry no PR, so the placeholder can be bound
/// unconditionally for every master. A run that HAS a PR number but no analysed
/// diff says so instead of rendering nothing: silence would read to the model as
/// "no changes", which is the one wrong answer.
/// </summary>
public static class PrDiffPromptSection
{
    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.TryGet<string>(ContextKeys.PrNumber, out var number);
        var hasDiff = pipeline.TryGet<PrDiffAnalysis>(ContextKeys.PrDiff, out var diff) && diff is not null;
        if (string.IsNullOrWhiteSpace(number) && !hasDiff) return string.Empty;

        pipeline.TryGet<string>(ContextKeys.PrAuthor, out var author);
        pipeline.TryGet<string>(ContextKeys.PrHead, out var head);
        pipeline.TryGet<string>(ContextKeys.PrBase, out var baseSha);
        var coordinates =
            $"PR #{number ?? "?"} by {author ?? "unknown"} — head {head ?? "?"}, base {baseSha ?? "?"}.";

        var body = hasDiff
            ? PrDiffPromptRenderer.Render(diff!)
            : "(no structured diff available — AnalyzePrDiff did not run; emit a single "
              + "blocking observation stating the diff is missing)";

        // The diff is untrusted input: it is authored by whoever opened the PR.
        return TicketPromptDelimiters.WrapSection(
            "## Pull request under review", $"{coordinates}\n\n{body}");
    }
}
