using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SkillRounds.Strategies;

/// <summary>
/// p0167b: pr-review domain section. Stable part: project brief + PR
/// coordinates + the structured diff (ContextKeys.PrDiff rendered with
/// new-file line numbers) + the line_range anchoring rule. The review skills
/// judge ONLY what the diff shows — no per-skill variable part.
/// </summary>
public sealed class PrReviewSkillPromptStrategy(IProjectBriefBuilder projectBriefBuilder) : ISkillPromptStrategy
{
    public string SkillRoundCommandName => "PrReviewSkillRoundCommand";

    public (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
    {
        var stable = $"""
            {projectBriefBuilder.Build(pipeline)}

            ## Pull Request Under Review
            {BuildPrCoordinates(pipeline)}

            {BuildDiffSection(pipeline)}

            Review ONLY the changed lines shown above (plus their immediate
            context). Anchor every observation with `file` + `line_range`
            ("start..end", inclusive) using the NEW-file line numbers printed
            in the diff. Do not review unchanged code, and do not cite line
            numbers that are not visible in the diff.
            """.Trim();
        return (stable, string.Empty);
    }

    private static string BuildPrCoordinates(PipelineContext pipeline)
    {
        pipeline.TryGet<string>(ContextKeys.PrNumber, out var number);
        pipeline.TryGet<string>(ContextKeys.PrAuthor, out var author);
        pipeline.TryGet<string>(ContextKeys.PrHead, out var head);
        pipeline.TryGet<string>(ContextKeys.PrBase, out var baseSha);
        return $"PR #{number ?? "?"} by {author ?? "unknown"} — head {head ?? "?"}, base {baseSha ?? "?"}.";
    }

    private static string BuildDiffSection(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PrDiffAnalysis>(ContextKeys.PrDiff, out var diff) || diff is null)
            return "## Diff\n(no structured diff available — AnalyzePrDiff did not run; emit a single blocking observation stating the diff is missing)";
        return $"## Diff\n{PrDiffPromptRenderer.Render(diff)}";
    }
}
