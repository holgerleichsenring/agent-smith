using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
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
/// 2026-09-13-a284: the pull request opens against the base its branch was cut from, and
/// one opened earlier against the wrong base is moved.
/// <para>
/// The pull request an epic child actually gets is opened at the SPEC commit, long before
/// CommitAndPR, and the commit-time path is find-OR-create — so a target on the create
/// path alone would pass its own test and never touch the path production takes. Both are
/// exercised here.
/// </para>
/// </summary>
public sealed class PullRequestTargetTests
{
    private const string Rung = "agent-smith/19100";
    private const string Branch = "agent-smith/19107";
    private const string PrUrl = "https://github.com/test/repo/pull/42";

    [Fact]
    public async Task SpecDraftPullRequest_OpensAgainstTheRung()
    {
        var provider = NewProvider();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(Branch), "https://x/repo.git"));
        var repo = new RepoConnection { Name = "server", Url = "https://x/repo.git" };
        PullRequestTargets.Record(pipeline, repo.Name, Rung);
        var sut = new SpecPullRequestOpener(
            FactoryFor(provider).Object, EventTestStubs.NoOp, NullLogger<SpecPullRequestOpener>.Instance);

        await sut.OpenAsync(pipeline, repo, EmptySet(), CancellationToken.None);

        provider.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), true,
            It.Is<BranchName?>(b => b != null && b.Value == Rung)), Times.Once);
    }

    [Fact]
    public async Task CommitAndPR_ExistingPullRequestOnAWrongBase_IsRetargeted()
    {
        var provider = NewProvider();
        provider.Setup(p => p.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrUrl);
        provider.Setup(p => p.ReadPullRequestBaseAsync(PrUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");

        await RunCommitAndPrAsync(provider, rung: Rung);

        provider.Verify(p => p.RetargetPullRequestAsync(
            PrUrl, It.Is<BranchName>(b => b.Value == Rung), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAndPR_ExistingPullRequestOnTheRung_IsNotRetargeted()
    {
        var provider = NewProvider();
        provider.Setup(p => p.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrUrl);
        provider.Setup(p => p.ReadPullRequestBaseAsync(PrUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Rung);

        await RunCommitAndPrAsync(provider, rung: Rung);

        provider.Verify(p => p.RetargetPullRequestAsync(
            It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CommitAndPR_NoRung_OpensAgainstTheProvidersOwnDefaultBranch()
    {
        var provider = NewProvider();

        await RunCommitAndPrAsync(provider, rung: null);

        provider.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(), null), Times.Once);
    }

    [Fact]
    public async Task PullRequestTarget_TwoRepos_EachGetsItsOwnRung()
    {
        // The opener loops repositories with ONE branch value, so a single recorded target
        // would send the second repository to a base its feature branch may not even have.
        var server = NewProvider();
        var client = NewProvider();
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "server"))).Returns(server.Object);
        factory.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "client"))).Returns(client.Object);
        var repos = new[]
        {
            new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = "https://x/server.git" },
            new RepoConnection { Name = "client", Type = RepoType.GitHub, Url = "https://x/client.git" },
        };
        var pipeline = PipelineWith(repos);
        PullRequestTargets.Record(pipeline, "server", Rung);
        // 'client' has no rung: this feature never reached that repository.

        await BuildCommitAndPrHandler(factory.Object).ExecuteAsync(
            ContextFor(repos, pipeline), CancellationToken.None);

        server.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(),
            It.Is<BranchName?>(b => b != null && b.Value == Rung)), Times.Once);
        client.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(), null), Times.Once);
    }

    [Fact]
    public async Task InitCommit_NoRung_OpensAsToday()
    {
        var provider = NewProvider();
        var repos = new[] { new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = "https://x/s.git" } };
        var pipeline = PipelineWith(repos);
        var handler = new InitCommitHandler(
            FactoryFor(provider).Object, NoTickets().Object, GitOps(),
            new TicketLifecycle(), new SandboxTargets(),
            NullLogger<InitCommitHandler>.Instance);

        await handler.ExecuteAsync(
            new InitCommitContext(
                new Repository(new BranchName(Branch), "https://x/s.git"),
                repos, new TrackerConnection(), pipeline),
            CancellationToken.None);

        provider.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(), null), Times.Once);
    }

    private async Task RunCommitAndPrAsync(Mock<ISourceProvider> provider, string? rung)
    {
        var repos = new[] { new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = "https://x/s.git" } };
        var pipeline = PipelineWith(repos);
        PullRequestTargets.Record(pipeline, "server", rung);

        await BuildCommitAndPrHandler(FactoryFor(provider).Object).ExecuteAsync(
            ContextFor(repos, pipeline), CancellationToken.None);
    }

    private static Mock<ISourceProvider> NewProvider()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.CreatePullRequestAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TicketId?>(), It.IsAny<bool>(),
                It.IsAny<BranchName?>()))
            .ReturnsAsync(PrUrl);
        provider.Setup(p => p.UpdatePullRequestBodyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        provider.Setup(p => p.RetargetPullRequestAsync(
                It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return provider;
    }

    private static Mock<ISourceProviderFactory> FactoryFor(Mock<ISourceProvider> provider)
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return factory;
    }

    private static Mock<ITicketProviderFactory> NoTickets()
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(new Mock<ITicketProvider>().Object);
        return factory;
    }

    private static PipelineContext PipelineWith(IReadOnlyList<RepoConnection> repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, repos);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            repos.ToDictionary(r => r.Name, _ => StagedSandbox(), StringComparer.Ordinal));
        return pipeline;
    }

    private static ISandbox StagedSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                var output = step.Args is not null && step.Args.Contains("diff") ? "+ staged change" : null;
                return Task.FromResult(
                    new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
            });
        return sandbox.Object;
    }

    private static CommitAndPRContext ContextFor(
        IReadOnlyList<RepoConnection> repos, PipelineContext pipeline) =>
        new(new Repository(new BranchName(Branch), "https://x/s.git"),
            [new CodeChange(new FilePath("README.md"), "content", "Created")],
            new Ticket(new TicketId("19107"), "Slice two", "Description", null, "Open", "GitHub"),
            repos, new TrackerConnection(), pipeline);

    private static CommitAndPRHandler BuildCommitAndPrHandler(ISourceProviderFactory sourceFactory) =>
        new(sourceFactory, NoTickets().Object, GitOps(), new SecretPatternScanner(),
            EventTestStubs.NoOp, new TicketLifecycle(), new SandboxTargets(),
            new PhaseAccounting(
                TestGit.Delivery,
                new SpecAccountant(
                    new ScriptedChatClientFactory(),
                    new AccountCalls(new SpecAccountCall(
                        new ScriptedChatClientFactory(),
                        new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(),
                        NullLogger<SpecAccountCall>.Instance)),
                    NullLogger<SpecAccountant>.Instance),
                new SandboxTargets(),
                NullLogger<PhaseAccounting>.Instance),
            new FailedRunPersistence(), new CompletedRunTicketSummary(),
            NullLogger<CommitAndPRHandler>.Instance);

    private static SandboxGitOperations GitOps() =>
        new(new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
            new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));

    private static SpecSet EmptySet() =>
        new("azdo-19107", [], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UnixEpoch)],
            SpecSource.Derived);
}
