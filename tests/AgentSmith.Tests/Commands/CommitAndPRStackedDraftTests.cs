using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-23-0219: a stacked run's pull request is opened as a DRAFT at the spec commit against its prerequisite's branch.
/// Azure's lookup searched the default branch only, so CommitAndPR never found it, created it again, got TF401179 back with
/// the draft's URL and reported it — ready never asked. Prod: PRs 9986, 9988 and 9991 stayed drafts after green runs. Pinned
/// through the real handler over the real Azure provider, with a Git client that answers as Azure DevOps does.
/// </summary>
public sealed class CommitAndPRStackedDraftTests
{
    private const string Rung = "agent-smith/19100";
    private const string Branch = "agent-smith/19107";
    private const string Url = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/9";

    private readonly GitPullRequest _draft = new()
    {
        PullRequestId = 9, IsDraft = true, Status = PullRequestStatus.Active,
        SourceRefName = $"refs/heads/{Branch}", TargetRefName = $"refs/heads/{Rung}",
    };

    [Fact]
    public async Task GreenStackedRun_FindsTheSpecCommitsDraft_AndTakesItOutOfDraft()
    {
        var git = AzureServer();

        var result = await RunAsync(Provider(git.Object));

        result.IsSuccess.Should().BeTrue(result.Message);
        _draft.IsDraft.Should().BeFalse("a complete, green run takes its pull request out of draft");
        git.Verify(c => c.CreatePullRequestAsync(It.IsAny<GitPullRequest>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never, "the draft is found, not re-created");
    }

    [Fact]
    public async Task CreateThatFindsTheDraftAlreadyOpen_TakesItOutOfDraft_WhenAReadyOneWasAskedFor()
    {
        var git = AzureServer();

        var url = await Provider(git.Object).CreatePullRequestAsync(new Repository(new BranchName(Branch), "https://x"), "T", "body",
            CancellationToken.None, isDraft: false, targetBranch: new BranchName(Rung));

        url.Should().Be(Url);
        _draft.IsDraft.Should().BeFalse();
    }

    [Fact]
    public async Task GreenRun_PublishedDraft_QueuesItsNotStartedBuildsAgain()
    {
        var provider = new Mock<ISourceProvider>();
        var requeuer = provider.As<IPullRequestBuildRequeuer>();
        provider.Setup(p => p.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>())).ReturnsAsync(Url);
        provider.Setup(p => p.UpdatePullRequestBodyAsync(Url, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        provider.Setup(p => p.MarkPullRequestReadyAsync(Url, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        requeuer.Setup(r => r.RequeueNotStartedAsync(Url, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        (await RunAsync(provider.Object)).IsSuccess.Should().BeTrue();

        requeuer.Verify(r => r.RequeueNotStartedAsync(Url, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Azure DevOps as the run meets it: one active draft on the branch, targeting the rung; a search that names another
    // target finds nothing, and a second create for the same source and target is refused with TF401179.
    private Mock<GitHttpClient> AzureServer()
    {
        var git = new Mock<GitHttpClient>(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        git.Setup(c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GitPullRequestSearchCriteria>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, GitPullRequestSearchCriteria c, int? _, int? _, int? _, object _, CancellationToken _) =>
                c.SourceRefName == _draft.SourceRefName && (c.TargetRefName is null || c.TargetRefName == _draft.TargetRefName) ? [_draft] : []);
        git.Setup(c => c.CreatePullRequestAsync(It.IsAny<GitPullRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TF401179: An active pull request for the source and target branch already exists."));
        git.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 9, It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _draft);
        git.Setup(c => c.UpdatePullRequestAsync(It.IsAny<GitPullRequest>(), It.IsAny<string>(), It.IsAny<string>(), 9,
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitPullRequest update, string _, string _, int _, object _, CancellationToken _) =>
            {
                if (update.IsDraft is { } draft) _draft.IsDraft = draft;
                return _draft;
            });
        return git;
    }

    private static ISourceProvider Provider(GitHttpClient git)
    {
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(git);
        factory.Setup(f => f.CreateGitClientAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(git);
        return new AzureReposSourceProvider(new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "sample-server", "pat", "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);
    }

    private static Task<CommandResult> RunAsync(ISourceProvider provider)
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider);
        var repos = new[] { new RepoConnection { Name = "sample-server", Type = RepoType.AzureDevOps, Url = "https://x/s.git" } };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, repos);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            repos.ToDictionary(r => r.Name, _ => StagedSandbox(), StringComparer.Ordinal));
        PullRequestTargets.Record(pipeline, repos[0].Name, Rung);
        return Handler(sources.Object).ExecuteAsync(new CommitAndPRContext(new Repository(new BranchName(Branch), "https://x/s.git"),
            [new CodeChange(new FilePath("README.md"), "content", "Created")],
            new Ticket(new TicketId("19107"), "Slice two", "Description", null, "Open", "AzureDevOps"),
            repos, new TrackerConnection(), pipeline), CancellationToken.None);
    }

    private static ISandbox StagedSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                step.Args is not null && step.Args.Contains("diff") ? "+ staged change" : null)));
        return sandbox.Object;
    }

    private static CommitAndPRHandler Handler(ISourceProviderFactory sources)
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(new Mock<ITicketProvider>().Object);
        var gitOps = new SandboxGitOperations(AgentSmith.Tests.TestSupport.TestPushers.Plain(), TestSupport.TestGitCredentials.Resolver,
            NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        return new CommitAndPRHandler(sources, tickets.Object, gitOps, new SecretPatternScanner(),
            new PullRequestOutcomePublisher(EventTestStubs.NoOp), new TicketLifecycle(), new SandboxTargets(),
            new FailedRunPersistence(new WipCommitMessage()),
            new SpecOnlyPullRequests(sources, new PullRequestOutcomePublisher(EventTestStubs.NoOp), NullLogger<SpecOnlyPullRequests>.Instance),
            new ShortfallDelivery(new UnverifiedWorkReverter(gitOps, NullLogger<UnverifiedWorkReverter>.Instance),
                NullLogger<ShortfallDelivery>.Instance),
            new CompletedRunTicketSummary(TestRunResultChanges.Create()), Structure.StructureGateFixture.Publisher(),
            TestSupport.SeriesSettingsDoubles.Banners(), TestSupport.SeriesSettingsDoubles.ChunkStop(), NullLogger<CommitAndPRHandler>.Instance,
            new PullRequestBuildRequeue(sources, NullLogger<PullRequestBuildRequeue>.Instance));
    }
}
