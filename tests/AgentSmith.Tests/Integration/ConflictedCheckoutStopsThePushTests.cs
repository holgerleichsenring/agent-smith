using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0496: the guard that keeps a stopped checkout out of the push path. The finalizer tail
/// runs on failure and PersistWorkBranch stages <c>git add -A</c> and force-pushes with a
/// lease — it bails only because CheckoutSourceHandler returns before ContextKeys.Repository
/// is set. That was invisible until this test; the merge above aborts the conflict, so the
/// two together are why no run can push a tree it could not reconcile.
/// </summary>
public sealed class ConflictedCheckoutStopsThePushTests
{
    private const string TicketBranch = "agent-smith/19106";

    [Fact]
    public async Task AConflictedMerge_PublishesNoRepository_SoPersistWorkBranchTouchesNothing()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);

        var config = new RepoConnection
        {
            Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath
        };
        var pipeline = PipelineWith(config, fixture);

        var checkout = await CheckoutHandler(config).ExecuteAsync(
            new CheckoutSourceContext(
                [config], new RunBranch(new BranchName(TicketBranch), ComposedFromTicket: true), pipeline),
            CancellationToken.None);

        checkout.IsSuccess.Should().BeFalse();
        checkout.Message.Should().Contain(GitRemoteFixture.SharedFile);
        pipeline.Has(ContextKeys.Repository).Should()
            .BeFalse("PersistWorkBranch reads this key and bails when it is absent");

        var persisted = await PersistHandler().ExecuteAsync(
            new PersistWorkBranchContext([config], new AgentConfig(), pipeline), CancellationToken.None);

        persisted.IsSuccess.Should().BeFalse();
        persisted.Message.Should().Contain("no Repository");
    }

    private static PipelineContext PipelineWith(RepoConnection config, GitRemoteFixture fixture)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [config]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["server"] = new InProcessSandbox(
                    jobId: "p0496", workDir: fixture.WorkPath, ownsWorkDir: false, NullLogger.Instance)
            });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["server"] = new RemoteContextDiscovery("default", ".", "csharp")
            });
        return pipeline;
    }

    private static CheckoutSourceHandler CheckoutHandler(RepoConnection config)
    {
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(TicketBranch), config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return new CheckoutSourceHandler(
            new SandboxRepoCloner(
                factory.Object, TestGit.Identity, TestGit.WorkBranchCheckout,
                NullLogger<SandboxRepoCloner>.Instance),
            RunStateConceptsTestFactory.Default,
            new SandboxTargets(), NullLogger<CheckoutSourceHandler>.Instance);
    }

    private static PersistWorkBranchHandler PersistHandler() =>
        new(new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
                new StubSandboxFileReaderFactory(), TestGit.Identity),
            new SandboxTargets(), NullLogger<PersistWorkBranchHandler>.Instance);
}
