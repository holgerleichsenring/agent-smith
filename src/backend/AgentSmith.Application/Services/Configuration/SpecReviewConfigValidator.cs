using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-10-8e59: the spec_review block at validation, as <see cref="SeriesConfigValidator"/> checks series — each
/// problem a Blocking finding naming its field, the one rule <see cref="SpecReviewConfig.Problems"/> states. The
/// settings store refuses the same block on save and on import, so no run meets it clamped to defaults.
/// </summary>
public static class SpecReviewConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.SpecReview.Problems().Select(problem => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, problem, Field: "spec_review"));
    }
}
