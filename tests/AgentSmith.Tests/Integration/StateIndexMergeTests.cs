using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-17-023fb against real git: two branches that each finished a spec both insert a state.done line under
/// done, and merging the base into the work branch used to stop the run on that collision. It now merges, with both
/// lines kept and no merge left open; a conflict anywhere else still stops as before.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class StateIndexMergeTests
{
    private const string TicketBranch = "agent-smith/77";
    private const string ContextYaml = ".agentsmith/contexts/default/context.yaml";
    private const string Base = "meta: { name: demo }\nstate:\n  done:\n    p0001: \"first\"\n  active: {}\n";

    [Fact]
    public async Task BothSidesAddedStateDoneLines_MergesWithBoth()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ContextYaml, Base);
        fixture.CommitOn(TicketBranch, ContextYaml, Base.Replace("  done:\n", "  done:\n    s-branch: \"the branch's spec\"\n"));
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ContextYaml, Base.Replace("  done:\n", "  done:\n    s-base: \"the base's spec\"\n"));

        var (checkout, sandbox) = await CheckOutAsync(fixture);

        checkout.Repository.Should().NotBeNull(checkout.Problem);
        var merged = fixture.ReadWorkFile(ContextYaml);
        merged.Should().Contain("s-branch: \"the branch's spec\"").And.Contain("s-base: \"the base's spec\"").And.Contain("p0001");
        merged.Should().NotContain("<<<<<<<");
        (await GitAsync(sandbox, "status", "--porcelain")).Trim().Should().BeEmpty("the merge is committed");
        File.Exists(Path.Combine(fixture.WorkPath, ".git", "MERGE_HEAD")).Should().BeFalse();
    }

    [Fact]
    public async Task AChangedBaseEntry_IsStillAConflict()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ContextYaml, Base);
        fixture.CommitOn(TicketBranch, ContextYaml, Base.Replace("\"first\"", "\"first, rewritten here\""));
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ContextYaml, Base.Replace("\"first\"", "\"first, rewritten there\""));

        var (checkout, _) = await CheckOutAsync(fixture);

        checkout.Repository.Should().BeNull();
        checkout.Problem.Should().Contain(ContextYaml);
    }

    private static async Task<string> GitAsync(ISandbox sandbox, params string[] args) =>
        (await sandbox.RunStepAsync(new AgentSmith.Sandbox.Wire.Step(AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion,
            Guid.NewGuid(), AgentSmith.Sandbox.Wire.StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: 60), progress: null, CancellationToken.None))
        .OutputContent ?? string.Empty;

    private static async Task<(RepoCheckout Checkout, ISandbox Sandbox)> CheckOutAsync(GitRemoteFixture fixture)
    {
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(TicketBranch), config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var sandbox = new InProcessSandbox(jobId: "023fb", workDir: fixture.WorkPath, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var cloner = new SandboxRepoCloner(factory.Object, AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            TestGit.Identity, TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance);
        var checkout = await cloner.CheckoutIntoSandboxesAsync(config, new RunBranch(new BranchName(TicketBranch), true, null),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
        return (checkout, sandbox);
    }
}
