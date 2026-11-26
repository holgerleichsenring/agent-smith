using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-26-462aa against real git: two branches that each finished a spec both add a line to done.md and to
/// MEMORY.md. With <c>merge=union</c> — committed in .gitattributes, and written by the merger into the clone's
/// info/attributes for a branch that predates it — merging the base into the work branch keeps both sides' lines,
/// commits, and needs no master. A conflict anywhere else still stops as before.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class IndexUnionMergeTests
{
    private const string TicketBranch = "agent-smith/77";
    private const string Done = ".agentsmith/contexts/default/done.md";
    private const string ContextYaml = ".agentsmith/contexts/default/context.yaml";
    private const string MemoryIndex = ".agentsmith/memory/MEMORY.md";
    private const string MemoryBase = "# Memory index\n\n- [a](a.md) (feedback) — first\n";
    private const string OldContext = "meta: { name: demo }\nstate:\n  done:\n    p0001: \"first\"\n  active: {}\n";
    private static readonly string DoneBase = ContextDoneIndex.Upsert(null, "p0001", "first");
    private static readonly string Attributes = IndexMergeAttributes.Ensure(null)!;

    [Fact]
    public async Task BothSidesAddedADoneLine_MergesWithBoth()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ".gitattributes", Attributes);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, DoneBase);
        fixture.CommitOn(TicketBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-branch", "the branch's spec"));
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-base", "the base's spec"));

        var (checkout, sandbox) = await CheckOutAsync(fixture);

        checkout.Repository.Should().NotBeNull(checkout.Problem);
        ContextDoneIndex.Entries(fixture.ReadWorkFile(Done)).Select(e => e.Key).Should().Equal("s-branch", "s-base", "p0001");
        fixture.ReadWorkFile(Done).Should().NotContain("<<<<<<<");
        (await GitAsync(sandbox, "status", "--porcelain")).Trim().Should().BeEmpty("the merge is committed");
        File.Exists(Path.Combine(fixture.WorkPath, ".git", "MERGE_HEAD")).Should().BeFalse();
    }

    [Fact]
    public async Task BothSidesMigratedTheSameContext_MergesWithBothLines()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, ContextYaml, OldContext);
        var extracted = new ContextYamlStateDoneCodec(new ContextYamlBuilders()).Extract(OldContext);
        var migrated = ContextDoneIndex.Append(null, extracted.Entries);
        foreach (var (branch, id) in new[] { (TicketBranch, "s-branch"), (GitRemoteFixture.BaseBranch, "s-base") })
        {
            fixture.CommitOn(branch, ".gitattributes", Attributes);
            fixture.CommitOn(branch, ContextYaml, extracted.Yaml!);
            fixture.CommitOn(branch, Done, ContextDoneIndex.Upsert(migrated, id, $"{id}'s spec"));
        }

        var (checkout, _) = await CheckOutAsync(fixture);

        checkout.Repository.Should().NotBeNull(checkout.Problem);
        ContextDoneIndex.Entries(fixture.ReadWorkFile(Done)).Select(e => e.Key).Should().Equal("s-branch", "s-base", "p0001");
        fixture.ReadWorkFile(ContextYaml).Should().Be(extracted.Yaml);
    }

    [Fact]
    public async Task ABranchWithoutTheAttributes_StillUnitesDoneAndMemoryLines()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, DoneBase);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, MemoryIndex, MemoryBase);
        fixture.CommitOn(TicketBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-branch", "b"));
        fixture.CommitOn(TicketBranch, MemoryIndex, MemoryBase + "- [b](b.md) (project) — the branch's\n");
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-base", "c"));
        fixture.CommitOn(GitRemoteFixture.BaseBranch, MemoryIndex, MemoryBase + "- [c](c.md) (project) — the base's\n");

        var (checkout, sandbox) = await CheckOutAsync(fixture);

        checkout.Repository.Should().NotBeNull(checkout.Problem);
        ContextDoneIndex.Entries(fixture.ReadWorkFile(Done)).Select(e => e.Key).Should().Equal("s-branch", "s-base", "p0001");
        fixture.ReadWorkFile(MemoryIndex).Should().Be(MemoryBase + "- [b](b.md) (project) — the branch's\n- [c](c.md) (project) — the base's\n");
        (await GitAsync(sandbox, "status", "--porcelain")).Trim().Should().BeEmpty("the merge is committed");
    }

    [Fact]
    public async Task IndexesAndACodeFileConflict_StopsNamingTheCodePath_TheTreeClean()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, DoneBase);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, "Program.cs", "var a = 1;\n");
        fixture.CommitOn(TicketBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-branch", "b"));
        fixture.CommitOn(TicketBranch, "Program.cs", "var a = 2;\n");
        fixture.CommitOn(GitRemoteFixture.BaseBranch, Done, ContextDoneIndex.Upsert(DoneBase, "s-base", "c"));
        fixture.CommitOn(GitRemoteFixture.BaseBranch, "Program.cs", "var a = 3;\n");

        var (checkout, sandbox) = await CheckOutAsync(fixture);

        checkout.Repository.Should().BeNull();
        checkout.Problem.Should().Contain("Program.cs").And.NotContain(Done);
        (await GitAsync(sandbox, "status", "--porcelain")).Trim().Should().BeEmpty("the merge was aborted");
    }

    private static async Task<string> GitAsync(ISandbox sandbox, params string[] args) =>
        (await sandbox.RunStepAsync(new AgentSmith.Sandbox.Wire.Step(AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion,
            Guid.NewGuid(), AgentSmith.Sandbox.Wire.StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.WorkspaceRoot, TimeoutSeconds: 60), progress: null, CancellationToken.None))
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
        var sandbox = new InProcessSandbox(jobId: "462aa", workDir: fixture.WorkPath, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var cloner = new SandboxRepoCloner(factory.Object, AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            TestGit.Identity, TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance);
        var checkout = await cloner.CheckoutIntoSandboxesAsync(config, new RunBranch(new BranchName(TicketBranch), true, null),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
        return (checkout, sandbox);
    }
}
