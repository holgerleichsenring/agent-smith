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
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-23-df05 (operator): a code run that delivered no spec keeps its pull request a draft. Prod run
/// 2026-11-22T22-58-27-c7db had one spec, a person answered "stop this spec", the master reported, the spec read done
/// and CommitAndPR published PR 9990. Pinned through the real handler: a spec recorded done over a branch that carried
/// no work leaves the PR — and the series repository's spec PR — in draft with no build re-queue; one that carried work
/// publishes as today.
/// </summary>
public sealed class CommitAndPRUndeliveredDraftTests
{
    private const string Url = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/9990";
    private const string SpecUrl = "https://dev.azure.com/example/demo/_git/sample-client/pullrequest/9991";

    private readonly Mock<ISourceProvider> _provider = new();
    private readonly Mock<IPullRequestBuildRequeuer> _requeuer;
    private string? _body;

    public CommitAndPRUndeliveredDraftTests()
    {
        _requeuer = _provider.As<IPullRequestBuildRequeuer>();
        _provider.Setup(p => p.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>())).ReturnsAsync(Url);
        _provider.Setup(p => p.UpdatePullRequestBodyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((url, body, _) => { if (url == Url) _body = body; })
            .ReturnsAsync(true);
        _provider.Setup(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _requeuer.Setup(r => r.RequeueNotStartedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task StoppedSpec_DoneWithoutWork_KeepsThePullRequestADraft_AndQueuesNoBuild()
    {
        (await RunAsync(carriedWork: false)).IsSuccess.Should().BeTrue();

        _provider.Verify(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _requeuer.Verify(r => r.RequeueNotStartedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _body.Should().StartWith(CodePullRequestBanner.NothingDelivered);
    }

    [Fact]
    public async Task DeliveredSpec_PublishesBothPullRequests_AndQueuesTheirBuilds_AsToday()
    {
        (await RunAsync(carriedWork: true)).IsSuccess.Should().BeTrue();

        _provider.Verify(p => p.MarkPullRequestReadyAsync(Url, It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(SpecUrl, It.IsAny<CancellationToken>()), Times.Once);
        _requeuer.Verify(r => r.RequeueNotStartedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _body.Should().NotContain("No spec delivered");
    }

    [Fact]
    public async Task RunWithoutASpecTable_PublishesAsToday()
    {
        (await RunAsync(carriedWork: null)).IsSuccess.Should().BeTrue();

        _provider.Verify(p => p.MarkPullRequestReadyAsync(Url, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Banner_AShortfallWhoseDoneSpecCarriedNoWork_SaysNothingWasDelivered()
    {
        var progress = new SpecSequenceProgress([new SpecProgress("a", "Stopped", SpecRunState.Done),
            new SpecProgress("b", "Red", SpecRunState.Failed)]);
        var shortfall = new RunShortfall([progress.Specs[0]], [progress.Specs[1]], "dotnet build exited 1");

        TestSupport.SeriesSettingsDoubles.Banners().For("dotnet build exited 1", shortfall, isDraft: true, progress)
            .Should().Be(CodePullRequestBanner.NothingDelivered);
    }

    private Task<CommandResult> RunAsync(bool? carriedWork)
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        RepoConnection[] repos =
        [
            new() { Name = "sample-server", Type = RepoType.AzureDevOps, Url = "https://x/s.git" },
            new() { Name = "sample-client", Type = RepoType.AzureDevOps, Url = "https://x/c.git" },
        ];
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, repos);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            [repos[0].Name] = Sandbox(staged: "+ staged change"), [repos[1].Name] = Sandbox(staged: null),
        });
        SpecPullRequests.Record(pipeline, repos[1].Name, SpecUrl);
        if (carriedWork is { } carried)
            pipeline.Set(ContextKeys.SpecSequenceProgress, new SpecSequenceProgress(
                [new SpecProgress("2026-11-19-0544a", "The spec a person stopped", SpecRunState.Done, CarriedWork: carried)]));
        return Handler(sources.Object).ExecuteAsync(new CommitAndPRContext(new Repository(new BranchName("agent-smith/19661"), "https://x/s.git"),
            [new CodeChange(new FilePath("README.md"), "content", "Created")],
            new Ticket(new TicketId("19661"), "Stopped spec", "Description", null, "Open", "AzureDevOps"),
            repos, new TrackerConnection(), pipeline), CancellationToken.None);
    }

    private static ISandbox Sandbox(string? staged)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                step.Args is not null && step.Args.Contains("diff") ? staged : null)));
        return sandbox.Object;
    }

    private static CommitAndPRHandler Handler(ISourceProviderFactory sources)
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(new Mock<ITicketProvider>().Object);
        var gitOps = new SandboxGitOperations(AgentSmith.Tests.TestSupport.TestPushers.Plain(), TestSupport.TestGitCredentials.Resolver,
            NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        var requeue = new PullRequestBuildRequeue(sources, NullLogger<PullRequestBuildRequeue>.Instance);
        return new CommitAndPRHandler(sources, tickets.Object, gitOps, new SecretPatternScanner(),
            new PullRequestOutcomePublisher(EventTestStubs.NoOp), new TicketLifecycle(), new SandboxTargets(),
            new FailedRunPersistence(new WipCommitMessage()),
            new SpecOnlyPullRequests(sources, new PullRequestOutcomePublisher(EventTestStubs.NoOp), NullLogger<SpecOnlyPullRequests>.Instance, requeue),
            new ShortfallDelivery(new UnverifiedWorkReverter(gitOps, NullLogger<UnverifiedWorkReverter>.Instance),
                NullLogger<ShortfallDelivery>.Instance),
            new CompletedRunTicketSummary(TestRunResultChanges.Create()), Structure.StructureGateFixture.Publisher(),
            TestSupport.SeriesSettingsDoubles.Banners(), TestSupport.SeriesSettingsDoubles.ChunkStop(), NullLogger<CommitAndPRHandler>.Instance,
            requeue);
    }
}
