using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-11-17-b34eb: the project's <c>merge:</c> block between the stored entry and the studio entity.
/// 2026-11-25-9992i: the block carries the CI reading keys only; when a pull request merges is the project's review.
/// </summary>
internal static class ProjectMergeMapping
{
    /// <summary>Null when the project declares no block — every key at its default.</summary>
    public static ProjectMerge? ToEntity(MergePolicyConfig? merge) =>
        merge is null
            ? null
            : new ProjectMerge(merge.CiPollSeconds, merge.CiSettleSeconds, merge.CiPendingSeconds, // 3435d
                merge.CiFixAttempts, merge.CiExcerptChars, merge.CiFailedChecksRead);

    /// <summary>The stored block for a sent one: each key as sent, a missing one at its default — the block is written whole.</summary>
    public static MergePolicyConfig ToRaw(ProjectMerge merge)
    {
        ArgumentNullException.ThrowIfNull(merge);
        var defaults = new MergePolicyConfig();
        return new MergePolicyConfig
        {
            CiPollSeconds = merge.CiPollSeconds ?? defaults.CiPollSeconds,
            CiSettleSeconds = merge.CiSettleSeconds ?? defaults.CiSettleSeconds,
            CiPendingSeconds = merge.CiPendingSeconds ?? defaults.CiPendingSeconds,
            CiFixAttempts = merge.CiFixAttempts ?? defaults.CiFixAttempts,
            CiExcerptChars = merge.CiExcerptChars ?? defaults.CiExcerptChars,
            CiFailedChecksRead = merge.CiFailedChecksRead ?? defaults.CiFailedChecksRead,
        };
    }

    /// <summary>2026-11-25-9992i: the stored review for a sent one — blank clears it to the default (end); an unknown value is
    /// refused by name with the allowed names, so a save never stores what the loader would refuse.</summary>
    public static string? ReviewToRaw(string projectId, string review)
    {
        if (string.IsNullOrWhiteSpace(review)) return null;
        if (ReviewModeNames.Parse(review) is null)
            throw new ConfigurationException(
                $"Project '{projectId}': review '{review}' is not a known mode ({string.Join(", ", ReviewModeNames.All)}).");
        return review.Trim();
    }
}
