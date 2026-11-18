using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-3435b: before a merge-green run merges with respect_branch_protection: true, the pull request is approved
/// where its host requires a review — as the identity merge.approval_auth names, else as the repo's own token. Bypass
/// needs no approval: the merge goes past the review rule. A refusal names the host's reason and the key that lends a
/// second identity; a provider that cannot approve (a local repository) needs none.
/// </summary>
public sealed class PullRequestApprovalStep(ISourceProviderFactory sources)
{
    public const string SecondIdentity =
        "set merge.approval_auth to the secret of an identity other than the pull request's author (the repo's token), "
        + "or respect_branch_protection: false to merge past the review rule";

    public async Task<PullRequestApproval> ApproveAsync(
        RepoConnection repo, string prUrl, MergePolicyConfig policy, CancellationToken ct)
    {
        if (policy.Protection == BranchProtection.Bypass) return PullRequestApproval.NotRequired;
        var identity = string.IsNullOrWhiteSpace(policy.ApprovalAuth) ? repo : repo with { Auth = policy.ApprovalAuth! };
        try
        {
            if (sources.Create(identity) is not IPullRequestApprover approver) return PullRequestApproval.NotRequired;
            var approval = await approver.ApproveIfRequiredAsync(prUrl, ct);
            return approval.Outcome == PullRequestApprovalOutcome.Refused ? Refused(approval.Reason) : approval;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Refused(ex.Message); }
    }

    private static PullRequestApproval Refused(string? reason) =>
        PullRequestApproval.Refused($"the host refused the approval: {reason ?? "no reason given"}; {SecondIdentity}");
}
