using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-26-5201f: whether an Azure DevOps pull request still waits for a person's approval — a blocking reviewer policy
/// (minimum reviewers, required reviewers) not approved — read with its state, never with its CI.
/// </summary>
public sealed class AzureReposApprovalTests
{
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";
    private static readonly Guid ProjectId = Guid.NewGuid();
    private readonly Mock<GitHttpClient> _git = new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
    private string? _artifact;

    public AzureReposApprovalTests()
    {
        _git.Setup(c => c.GetThreadsAsync("demo", "sample-server", 7, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Pr(PullRequestStatus.Active);
    }

    [Theory]
    [InlineData(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Queued, true, true)]
    [InlineData(AzureReposApprovalPolicies.RequiredReviewersType, PolicyEvaluationStatus.Running, true, true)]
    [InlineData(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Rejected, true, true)]
    [InlineData(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Approved, true, false)]
    [InlineData(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.NotApplicable, true, false)]
    [InlineData(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Queued, false, false)]
    [InlineData(AzureReposCiPolicies.BuildPolicyType, PolicyEvaluationStatus.Queued, true, false)]
    public void Required_IsABlockingReviewerPolicyNotApproved(string type, PolicyEvaluationStatus status, bool blocking, bool expected) =>
        AzureReposApprovalPolicies.Required([Reviewer(type, status, blocking)]).Should().Be(expected);

    // The reported case: the build policy passed (or there is none) and a blocking reviewer policy waits — the state read says a
    // person's approval is required, which the hold turns into approval_needed instead of CI running.
    [Fact]
    public async Task StateRead_ABuildPassedAndAReviewerPolicyPending_RequiresAnApproval()
    {
        var state = await ReadAsync(Build(PolicyEvaluationStatus.Approved), Reviewer(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Queued));

        state.ApprovalRequired.Should().BeTrue();
        _artifact.Should().Be($"vstfs:///CodeReview/CodeReviewId/{ProjectId}/7");
        (await ReadAsync(Build(PolicyEvaluationStatus.Approved))).ApprovalRequired.Should().BeFalse();
        (await ReadAsync()).ApprovalRequired.Should().BeFalse("no policy asks for a reviewer");
    }

    [Fact]
    public async Task StateRead_AnEndedPullRequest_OrEvaluationsThatCannotBeRead_SayNothing()
    {
        (await Reads((_, _, _) => throw new InvalidOperationException("policy read failed")).ReadAsync(PrUrl, default)).ApprovalRequired.Should().BeNull();
        (await new AzureReposReviewReads(_ => Task.FromResult(_git.Object), _ => Task.FromResult<string?>("self"), "demo", "sample-server", "u")
            .ReadAsync(PrUrl, default)).ApprovalRequired.Should().BeNull("a read without the evaluations cannot tell");
        Pr(PullRequestStatus.Completed);
        (await ReadAsync(Reviewer(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Queued))).ApprovalRequired.Should().BeNull();
    }

    internal static PolicyEvaluationRecord Reviewer(string type, PolicyEvaluationStatus status, bool blocking = true) => new()
    {
        Status = status,
        Configuration = new PolicyConfiguration { IsBlocking = blocking, Type = new PolicyTypeRef { Id = Guid.Parse(type) } },
        Context = new JObject(),
    };

    private static PolicyEvaluationRecord Build(PolicyEvaluationStatus status) => Reviewer(AzureReposCiPolicies.BuildPolicyType, status);

    private Task<AgentSmith.Contracts.Reviews.PrReviewState> ReadAsync(params PolicyEvaluationRecord[] evaluations) =>
        Reads((_, artifact, _) => { _artifact = artifact; return Task.FromResult<IReadOnlyList<PolicyEvaluationRecord>>(evaluations); })
            .ReadAsync(PrUrl, default);

    private AzureReposReviewReads Reads(AzureReposPolicyEvaluations policies) =>
        new(_ => Task.FromResult(_git.Object), _ => Task.FromResult<string?>("self"), "demo", "sample-server", "u", NullLogger.Instance, policies);

    private void Pr(PullRequestStatus status) =>
        _git.Setup(c => c.GetPullRequestAsync("demo", "sample-server", 7, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 7, Status = status, MergeStatus = PullRequestAsyncStatus.Queued, CreationDate = DateTime.UtcNow,
                Repository = new GitRepository { ProjectReference = new TeamProjectReference { Id = ProjectId } },
            });
}
