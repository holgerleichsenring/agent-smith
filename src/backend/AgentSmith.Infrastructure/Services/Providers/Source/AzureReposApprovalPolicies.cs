using Microsoft.TeamFoundation.Policy.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-26-5201f: whether an Azure DevOps pull request still waits for a person's approval — a blocking evaluation of the
/// built-in Minimum number of reviewers or Required reviewers policy that is not approved (queued, running, rejected or broken).
/// Not applicable decides nothing. A reviewer policy is never CI: <see cref="AzureReposCiPolicies"/> reads the Build type only.
/// </summary>
public static class AzureReposApprovalPolicies
{
    /// <summary>Azure DevOps' built-in "Minimum number of reviewers" policy type.</summary>
    public const string MinimumReviewersType = "fa4e907d-c16b-4a4c-9dfa-4906e5d171dd";

    /// <summary>Azure DevOps' built-in "Required reviewers" policy type.</summary>
    public const string RequiredReviewersType = "fd2167ab-b0be-447a-8ec8-39368250530e";

    public static bool Required(IReadOnlyList<PolicyEvaluationRecord> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return evaluations.Any(e => e.Configuration is { IsBlocking: true, Type.Id: var type } && IsReviewerType(type)
            && e.Status is { } status && status is not (PolicyEvaluationStatus.Approved or PolicyEvaluationStatus.NotApplicable));
    }

    private static bool IsReviewerType(Guid type) =>
        type == Guid.Parse(MinimumReviewersType) || type == Guid.Parse(RequiredReviewersType);
}
