using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-f9c5e: Azure's answer to a completion that conflicts with its base is a refusal that says so.</summary>
public sealed class AzureReposConflictInterpretTests
{
    [Fact]
    public void Interpret_Conflicts_IsARefusalThatConflicts_APolicyRejectionIsNot()
    {
        AzureReposPolicyBypass.Interpret(new GitPullRequest { MergeStatus = PullRequestAsyncStatus.Conflicts }, 7)
            .Should().Match<PullRequestCompletion>(c => c.Outcome == PullRequestCompletionOutcome.Refused && c.Conflicts);
        AzureReposPolicyBypass.Interpret(new GitPullRequest { MergeStatus = PullRequestAsyncStatus.RejectedByPolicy }, 7).Conflicts
            .Should().BeFalse();
    }
}
