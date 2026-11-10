using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-10-13-8762v: a series or spec_review block the run would refuse is never stored — the one rule, read where
/// it is used. 2026-11-10-8e59: shared by a settings save and a YAML import, so neither path stores what the other
/// refuses.
/// </summary>
internal static class SettingsBlockRule
{
    public static IReadOnlyList<string> Problems(object value) =>
        value switch { SeriesConfig series => series.Problems(), SpecReviewConfig review => review.Problems(), _ => [] };

    public static void Validate(RawAgentSmithConfig raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        IReadOnlyList<string> problems = [.. Problems(raw.Series), .. Problems(raw.SpecReview)];
        if (problems.Count > 0)
            throw new ConfigurationException($"Invalid settings in the imported file: {string.Join("; ", problems)}");
    }
}
