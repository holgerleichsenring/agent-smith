using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-11-17-b34eb: the project's <c>merge:</c> block between the stored entry and the studio entity. The mode
/// crosses as the snake_case name the YAML uses, so the form offers exactly the names the capabilities list.
/// </summary>
internal static class ProjectMergeMapping
{
    /// <summary>Null when the project declares no block — absence, which reads as manual.</summary>
    public static ProjectMerge? ToEntity(MergePolicyConfig? merge) =>
        merge is null
            ? null
            : new ProjectMerge(ConfigStudioCapabilities.SnakeName(merge.When), merge.RespectBranchProtection,
                merge.RefreshOpenPullRequests, merge.CiPollSeconds, merge.CiSettleSeconds, merge.CiPendingSeconds, // 3435d
                merge.CiFixAttempts, merge.CiExcerptChars, merge.CiFailedChecksRead, merge.ApprovalAuth);

    /// <summary>The stored block for a sent one; an unknown mode is refused by name.</summary>
    public static MergePolicyConfig ToRaw(string projectId, ProjectMerge merge)
    {
        ArgumentNullException.ThrowIfNull(merge);
        var when = Enum.GetValues<MergeWhen>()
            .Where(w => ConfigStudioCapabilities.SnakeName(w) == merge.When?.Trim().ToLowerInvariant())
            .Cast<MergeWhen?>().FirstOrDefault()
            ?? throw new ConfigurationException(
                $"Project '{projectId}': merge.when '{merge.When}' is not a known mode " +
                $"({string.Join(", ", ConfigStudioCapabilities.MergeWhenNames)}).");
        var defaults = new MergePolicyConfig();
        return new MergePolicyConfig
        {
            When = when,
            RespectBranchProtection = merge.RespectBranchProtection,
            RefreshOpenPullRequests = merge.RefreshOpenPullRequests,
            // 2026-11-17-3435d: the CI loop's keys as sent, a missing one at its default — the block is written whole.
            CiPollSeconds = merge.CiPollSeconds ?? defaults.CiPollSeconds,
            CiSettleSeconds = merge.CiSettleSeconds ?? defaults.CiSettleSeconds,
            CiPendingSeconds = merge.CiPendingSeconds ?? defaults.CiPendingSeconds,
            CiFixAttempts = merge.CiFixAttempts ?? defaults.CiFixAttempts,
            CiExcerptChars = merge.CiExcerptChars ?? defaults.CiExcerptChars,
            CiFailedChecksRead = merge.CiFailedChecksRead ?? defaults.CiFailedChecksRead,
            ApprovalAuth = string.IsNullOrWhiteSpace(merge.ApprovalAuth) ? null : merge.ApprovalAuth.Trim(),
        };
    }
}
