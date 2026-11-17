using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: an Azure DevOps pull request's CI — build-validation policies tied to the head through their build,
/// and the statuses of the latest iteration. A rejected build is read with its failed task's issues and log end.
/// </summary>
public sealed class AzureReposCiReaderTests
{
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";
    private static readonly Guid ProjectId = Guid.NewGuid();
    private readonly Mock<GitHttpClient> _git = Client<GitHttpClient>();
    private IReadOnlyList<PolicyEvaluationRecord> _evaluations = [];
    private string? _artifact;
    private readonly Mock<BuildHttpClient> _build = Client<BuildHttpClient>();

    public AzureReposCiReaderTests()
    {
        _git.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 7, LastMergeSourceCommit = new GitCommitRef { CommitId = "abc" },
                Repository = new GitRepository { ProjectReference = new TeamProjectReference { Id = ProjectId } },
            });
        _git.Setup(c => c.GetPullRequestIterationsAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GitPullRequestIteration { Id = 1 }, new GitPullRequestIteration { Id = 2 }]);
        Statuses();
        Evaluations();
        _build.Setup(c => c.GetBuildAsync(ProjectId, 55, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build { Id = 55, SourceVersion = "merge", TriggerInfo = { ["pr.sourceSha"] = "abc" } });
        _build.Setup(c => c.GetBuildTimelineAsync(ProjectId, 55, It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailedTimeline());
        _build.Setup(c => c.GetBuildLogLinesAsync(ProjectId, 55, 9, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["restore", "Failed!  - Failed: 1, Passed: 41"]);
    }

    [Fact]
    public async Task RejectedBuildPolicy_IsRed_WithTheFailedTaskIssuesLogEndAndLink()
    {
        Evaluations(Evaluation(PolicyEvaluationStatus.Rejected));

        var ci = await ReadAsync();

        ci.State.Should().Be(CiStates.Red);
        var failure = ci.Failures.Single();
        failure.Name.Should().Be("PR build");
        failure.Url.Should().EndWith("_build/results?buildId=55");
        _artifact.Should().Be($"vstfs:///CodeReview/CodeReviewId/{ProjectId}/7");
        failure.Excerpt.Should().Contain("task failed: dotnet test").And.Contain("expected 3, was 4").And.EndWith("Failed: 1, Passed: 41");
    }

    [Fact]
    public async Task RunningBuild_IsPending_ApprovedIsGreen()
    {
        Evaluations(Evaluation(PolicyEvaluationStatus.Running));
        (await ReadAsync()).State.Should().Be(CiStates.Pending);
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));
        (await ReadAsync()).State.Should().Be(CiStates.Green);
    }

    [Fact]
    public async Task ApprovedBuildOfAnOlderHead_IsPending()
    {
        _build.Setup(c => c.GetBuildAsync(ProjectId, 55, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build { Id = 55, TriggerInfo = { ["pr.sourceSha"] = "older" } });
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));

        (await ReadAsync()).State.Should().Be(CiStates.Pending);
    }

    [Fact]
    public async Task FailedStatusOfTheLatestIteration_IsRed_AnOlderIterationsIsIgnored()
    {
        Statuses(Status(1, GitStatusState.Failed), Status(2, GitStatusState.Failed, "lint broke"));

        var ci = await ReadAsync();

        ci.State.Should().Be(CiStates.Red);
        ci.Failures.Single().Excerpt.Should().Be("lint broke");
    }

    [Fact]
    public async Task NoPolicyNoStatus_IsNone() => (await ReadAsync()).State.Should().Be(CiStates.None);

    private Task<PullRequestCi> ReadAsync() =>
        new AzureReposCiReader(_ => Task.FromResult(_git.Object), (_, artifact, _) => { _artifact = artifact; return Task.FromResult(_evaluations); }, _ => Task.FromResult(_build.Object),
            "demo", "sample-server", "https://dev.azure.com/example").ReadAsync(PrUrl, new CiReadRequest(null, 4000, 5), CancellationToken.None);

    private void Evaluations(params PolicyEvaluationRecord[] records) => _evaluations = records;

    private void Statuses(params GitPullRequestStatus[] statuses) =>
        _git.Setup(c => c.GetPullRequestStatusesAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. statuses]);

    private static PolicyEvaluationRecord Evaluation(PolicyEvaluationStatus status) => new()
    {
        Status = status,
        Configuration = new PolicyConfiguration
        {
            IsBlocking = true, Type = new PolicyTypeRef { Id = AzureReposCiPolicies.BuildPolicyType },
            Settings = new JObject { ["displayName"] = "PR build" },
        },
        Context = new JObject { ["buildId"] = 55, ["isExpired"] = false },
    };

    private static GitPullRequestStatus Status(int iteration, GitStatusState state, string description = "old") => new()
    {
        Id = iteration, IterationId = iteration, State = state, Description = description,
        Context = new GitStatusContext { Genre = "ci", Name = "lint" }, CreationDate = DateTime.UtcNow,
    };

    // The SDK's Timeline has no public constructor; the client builds it from JSON.
    private static Timeline FailedTimeline()
    {
        var timeline = (Timeline)Activator.CreateInstance(typeof(Timeline), nonPublic: true)!;
        timeline.Records.Add(new TimelineRecord
        {
            Name = "dotnet test", RecordType = "Task", Result = TaskResult.Failed, Log = new BuildLogReference { Id = 9 },
            Issues = { new Issue { Type = IssueType.Error, Message = "CalcTests.Add: expected 3, was 4" } },
        });
        return timeline;
    }

    private static Mock<T> Client<T>() where T : class =>
        new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
}
