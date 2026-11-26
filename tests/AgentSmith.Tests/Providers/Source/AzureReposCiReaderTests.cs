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

    // 2026-11-23-0219: Azure queues nothing on a draft, and a published or retargeted pull request may wait so — an expired
    // evaluation or one queued with no build on the head has not started; a running build, or a queued build of the head, runs.
    [Fact]
    public async Task ExpiredOrQueuedWithoutABuild_IsNotStarted_ARunningOrQueuedHeadBuild_IsPending()
    {
        var expired = Evaluation(PolicyEvaluationStatus.Approved);
        expired.Context!["isExpired"] = true;
        Evaluations(expired);
        (await ReadAsync()).State.Should().Be(CiStates.NotStarted);

        var queued = Evaluation(PolicyEvaluationStatus.Queued);
        queued.Context!.Remove("buildId");
        Evaluations(queued);
        (await ReadAsync()).State.Should().Be(CiStates.NotStarted);

        Evaluations(Evaluation(PolicyEvaluationStatus.Queued));
        (await ReadAsync()).State.Should().Be(CiStates.Pending, "build 55 ran the head and is queued in the pool");
    }

    [Fact]
    public async Task QueuedWithABuildOfAnOlderHead_IsNotStarted()
    {
        _build.Setup(c => c.GetBuildAsync(ProjectId, 55, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build { Id = 55, TriggerInfo = { ["pr.sourceSha"] = "older" } });
        Evaluations(Evaluation(PolicyEvaluationStatus.Queued));

        (await ReadAsync()).State.Should().Be(CiStates.NotStarted);
    }

    [Fact]
    public async Task Requeue_QueuesOnlyTheEvaluationsThatHaveNotStarted()
    {
        var notStarted = Evaluation(PolicyEvaluationStatus.Queued);
        notStarted.Context!.Remove("buildId");
        notStarted.EvaluationId = Guid.NewGuid();
        var running = Evaluation(PolicyEvaluationStatus.Running);
        running.EvaluationId = Guid.NewGuid();
        Evaluations(notStarted, running);
        var queued = new List<Guid>();

        var count = await new AzureReposBuildRequeue(_ => Task.FromResult(_git.Object), (_, _, _) => Task.FromResult(_evaluations),
                (_, id, _) => { queued.Add(id); return Task.CompletedTask; }, _ => Task.FromResult(_build.Object),
                "demo", "sample-server", "https://dev.azure.com/example", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .RequeueAsync(PrUrl, CancellationToken.None);

        count.Should().Be(1);
        queued.Should().Equal(notStarted.EvaluationId);
    }

    [Fact]
    public async Task FailedStatusOfTheLatestIteration_IsRed_AnOlderIterationsIsIgnored()
    {
        Statuses(Status(1, GitStatusState.Failed), Status(2, GitStatusState.Failed, "lint broke"));

        var ci = await ReadAsync();

        ci.State.Should().Be(CiStates.Red);
        ci.Failures.Single().Excerpt.Should().Be("lint broke");
    }

    // 2026-11-25-ef94: a coverage check posts pending and then not applicable on every push; the newest one decides.
    [Fact]
    public async Task StatusWhoseNewestIsNotApplicable_IsNoCheck_TheEarlierPendingDoesNotStand()
    {
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));
        var pending = Status(2, GitStatusState.Pending);
        var notApplicable = Status(2, GitStatusState.NotApplicable);
        notApplicable.Id = 3;
        Statuses(pending, notApplicable);

        (await ReadAsync()).State.Should().Be(CiStates.Green);
    }

    // 2026-11-23-c785: Azure files an evaluation with buildId 0 when no build stands behind it; build 0 is never asked for,
    // and the evaluation's own status is the check (asking threw and kept a stale "running" for days).
    [Fact]
    public async Task BuildIdZero_IsNoBuild_TheEvaluationStatusDecides()
    {
        var approved = Evaluation(PolicyEvaluationStatus.Approved);
        approved.Context!["buildId"] = 0;
        Evaluations(approved);

        (await ReadAsync()).State.Should().Be(CiStates.Green);
        _build.Verify(c => c.GetBuildAsync(ProjectId, 0, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoPolicyNoStatus_IsNone() => (await ReadAsync()).State.Should().Be(CiStates.None);

    // 2026-11-26-5201f: a status whose newest state is NotSet set no state at all — it is no check, never CI running for good.
    [Fact]
    public async Task StatusWhoseNewestIsNotSet_IsNoCheck()
    {
        Statuses(Status(2, GitStatusState.NotSet));
        (await ReadAsync()).State.Should().Be(CiStates.None);

        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));
        (await ReadAsync()).State.Should().Be(CiStates.Green);
    }

    // 2026-11-26-5201f: the reported case — the build policy passed, a blocking reviewer policy waits for a person. The reviewer
    // policy is no CI: the head is green, not running (whether it waits for an approval is the state read's, AzureReposApprovalTests).
    [Fact]
    public async Task BuildPassed_AReviewerPolicyPending_IsGreen_NotRunning()
    {
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved), AzureReposApprovalTests.Reviewer(AzureReposApprovalPolicies.MinimumReviewersType, PolicyEvaluationStatus.Queued));
        (await ReadAsync()).State.Should().Be(CiStates.Green);

        Evaluations(AzureReposApprovalTests.Reviewer(AzureReposApprovalPolicies.RequiredReviewersType, PolicyEvaluationStatus.Running));
        (await ReadAsync()).State.Should().Be(CiStates.None);
    }

    // 2026-11-26-5201f: a pull request into a series branch is validated by the default branch's builds on its head; a status no
    // policy requires no longer hides them — before, any status kept them unread and a pending one decided alone.
    [Fact]
    public async Task SeriesBranchWithANonRequiredPendingStatus_ReadsTheValidatingBuilds()
    {
        SeriesTarget();
        Statuses(Status(2, GitStatusState.Pending));
        _build.Setup(c => c.GetBuildsAsync(ProjectId, It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<BuildReason?>(), It.IsAny<BuildStatus?>(),
                It.IsAny<BuildResult?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<QueryDeletedOption?>(), It.IsAny<BuildQueryOrder?>(), It.IsAny<string>(), It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Build { Id = 80, Definition = new DefinitionReference { Id = 12 }, SourceVersion = "abc",
                Status = BuildStatus.Completed, Result = BuildResult.Succeeded }]);

        var ci = await ReadAsync((_, _) => Task.FromResult<IReadOnlyList<PolicyConfiguration>>([ValidatingMain(12)]));

        ci.State.Should().Be(CiStates.Green, "the required validating build passed; the optional status decides nothing beside it");
    }

    // Addendum: a pull-request build's source version is the test merge. One without pr.sourceSha that built the head's test merge
    // ran the head — it read pending for good; one of another merge still waits.
    [Fact]
    public async Task ApprovedBuildOfTheHeadsTestMerge_WithoutSourceSha_IsGreen()
    {
        TestMerge("merge-of-abc");
        _build.Setup(c => c.GetBuildAsync(ProjectId, 55, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build { Id = 55, SourceVersion = "merge-of-abc" });
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));
        (await ReadAsync()).State.Should().Be(CiStates.Green);

        TestMerge("merge-of-newer");
        (await ReadAsync()).State.Should().Be(CiStates.Pending, "a build of another merge did not run this head's");
    }

    // Addendum: a build that cannot be fetched (deleted, retained away) failed the whole read and kept the stored pending.
    [Fact]
    public async Task ABuildThatCannotBeFetched_LeavesTheEvaluationsStatus()
    {
        _build.Setup(c => c.GetBuildAsync(ProjectId, 55, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("build 55 not found"));
        Evaluations(Evaluation(PolicyEvaluationStatus.Approved));

        (await ReadAsync()).State.Should().Be(CiStates.Green);
    }

    private void TestMerge(string merge) =>
        _git.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 7, LastMergeSourceCommit = new GitCommitRef { CommitId = "abc" }, LastMergeCommit = new GitCommitRef { CommitId = merge },
                Repository = new GitRepository { ProjectReference = new TeamProjectReference { Id = ProjectId } },
            });

    private void SeriesTarget() =>
        _git.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 7, LastMergeSourceCommit = new GitCommitRef { CommitId = "abc" },
                SourceRefName = "refs/heads/agent-smith/7", TargetRefName = "refs/heads/agent-smith/series/s1",
                Repository = new GitRepository { Id = RepoId, ProjectReference = new TeamProjectReference { Id = ProjectId } },
            });

    private static readonly Guid RepoId = Guid.NewGuid();

    private static PolicyConfiguration ValidatingMain(int definition) => new()
    {
        IsEnabled = true, IsBlocking = true, Type = new PolicyTypeRef { Id = Guid.Parse(AzureReposCiPolicies.BuildPolicyType) },
        Settings = JObject.FromObject(new
        {
            buildDefinitionId = definition, displayName = "PR build",
            scope = new[] { new { repositoryId = RepoId.ToString(), refName = "refs/heads/main", matchKind = "exact" } },
        }),
    };

    private Task<PullRequestCi> ReadAsync(AzureReposPolicyConfigurations? configurations = null) =>
        new AzureReposCiReader(_ => Task.FromResult(_git.Object), (_, artifact, _) => { _artifact = artifact; return Task.FromResult(_evaluations); }, _ => Task.FromResult(_build.Object),
            "demo", "sample-server", "https://dev.azure.com/example", configurations, _ => Task.FromResult("main"))
            .ReadAsync(PrUrl, new CiReadRequest(null, 4000, 5), CancellationToken.None);

    private void Evaluations(params PolicyEvaluationRecord[] records) => _evaluations = records;

    private void Statuses(params GitPullRequestStatus[] statuses) =>
        _git.Setup(c => c.GetPullRequestStatusesAsync(It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. statuses]);

    private static PolicyEvaluationRecord Evaluation(PolicyEvaluationStatus status) => new()
    {
        Status = status,
        Configuration = new PolicyConfiguration
        {
            IsBlocking = true, Type = new PolicyTypeRef { Id = Guid.Parse(AzureReposCiPolicies.BuildPolicyType) },
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
