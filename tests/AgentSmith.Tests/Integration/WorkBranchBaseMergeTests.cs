using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0496 against real git: a re-run that reuses a ticket's work branch brings the base
/// branch's newer commits with it, a first run's branch creation takes no merge, and a
/// conflicting merge stops the run without changing the branch.
/// <para>
/// 2026-09-13-5cdf: and the base both halves use is the one the LADDER resolved — the
/// parent's rung when the clone has it. Asked of real git, because whether a branch was
/// cut from a rung is a fact about the tree, not about the argument list.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class WorkBranchBaseMergeTests
{
    private const string TicketBranch = "agent-smith/19106";
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";

    [Fact]
    public async Task ReusedWorkBranch_BaseHasNewerCommits_AreMergedIn()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch);
        fixture.AdvanceBase();

        var (checkout, sandbox) = await CheckOutAsync(fixture, TicketBranch, composedFromTicket: true);

        checkout.Repository.Should().NotBeNull();
        fixture.WorkFileExists(GitRemoteFixture.BaseOnlyFile).Should()
            .BeTrue("the file the branch was cut before must arrive with the base merge");
        fixture.WorkFileExists(GitRemoteFixture.WorkOnlyFile).Should()
            .BeTrue("the earlier run's committed work is built on, not discarded");
        sandbox.Ran("merge", $"origin/{GitRemoteFixture.BaseBranch}").Should().BeTrue();
    }

    [Fact]
    public async Task ReusedWorkBranch_AlreadyUpToDate_IsLeftUnchanged()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch);

        var (checkout, sandbox) = await CheckOutAsync(fixture, TicketBranch, composedFromTicket: true);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("merge", $"origin/{GitRemoteFixture.BaseBranch}").Should()
            .BeFalse("a branch that already contains its base has nothing to merge");
        fixture.ReadWorkFile(GitRemoteFixture.WorkOnlyFile).Should().Be("work\n");
    }

    [Fact]
    public async Task ReusedWorkBranch_MergeConflicts_RunStops_AndNamesThePathsAndRefs()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);

        var (checkout, _) = await CheckOutAsync(fixture, TicketBranch, composedFromTicket: true);

        checkout.Repository.Should().BeNull("a half-merged tree builds nothing");
        checkout.Problem.Should().Contain(GitRemoteFixture.SharedFile)
            .And.Contain($"origin/{GitRemoteFixture.BaseBranch}")
            .And.Contain(TicketBranch);
    }

    [Fact]
    public async Task ReusedWorkBranch_MergeConflicts_BranchIsLeftUntouched()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);

        var (_, sandbox) = await CheckOutAsync(fixture, TicketBranch, composedFromTicket: true);

        sandbox.Ran("merge", "--abort").Should().BeTrue();
        fixture.ReadWorkFile(GitRemoteFixture.SharedFile).Should()
            .Be("the branch rewrote it\n", "an aborted merge restores the branch exactly");
        var status = await GitAsync(sandbox, "status", "--porcelain");
        status.Trim().Should().BeEmpty("a tree with conflict markers must never reach the push path");
    }

    [Fact]
    public async Task FreshWorkBranch_CreatedFromBase_TakesNoMerge()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);

        var (checkout, sandbox) = await CheckOutAsync(fixture, TicketBranch, composedFromTicket: true);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("checkout", "-b", TicketBranch).Should().BeTrue();
        sandbox.Ran("merge").Should().BeFalse("a branch created from the base is already at the base");
    }

    [Fact]
    public async Task AHandedInBranch_IsNeverMergedInto()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        const string contributorBranch = "feature/somebody-elses-pr";
        await using var fixture = GitRemoteFixture.Create(contributorBranch);
        fixture.AdvanceBase();

        var (checkout, sandbox) = await CheckOutAsync(fixture, contributorBranch, composedFromTicket: false);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("merge").Should()
            .BeFalse("a PR head branch belongs to its author — the push path force-pushes with a lease");
        fixture.WorkFileExists(GitRemoteFixture.BaseOnlyFile).Should().BeFalse();
    }

    [Fact]
    public async Task WorkBranch_CutFromTheResolvedRung()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null, rung: Rung);

        var (checkout, sandbox) = await CheckOutAsync(
            fixture, TicketBranch, composedFromTicket: true, parentTicketId: ParentTicket);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("checkout", $"origin/{Rung}").Should()
            .BeTrue("the rung is checked out BEFORE the work branch is created from it");
        sandbox.Ran("checkout", "-b", TicketBranch).Should().BeTrue();
        fixture.WorkFileExists(GitRemoteFixture.RungOnlyFile).Should()
            .BeTrue("a branch cut from the default branch would not carry the feature's own work");
    }

    [Fact]
    public async Task BaseMerger_MergesTheRungNotOriginHead()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(TicketBranch, rung: Rung);
        fixture.AdvanceBase();
        fixture.AdvanceRung(Rung);

        var (checkout, sandbox) = await CheckOutAsync(
            fixture, TicketBranch, composedFromTicket: true, parentTicketId: ParentTicket);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("merge", $"origin/{Rung}").Should().BeTrue();
        fixture.WorkFileExists(GitRemoteFixture.RungLaterFile).Should()
            .BeTrue("a reused slice is brought up to date with the rung it was cut from");
        fixture.WorkFileExists(GitRemoteFixture.BaseOnlyFile).Should()
            .BeFalse("merging the DEFAULT branch into a slice dissolves the isolation the rung exists for");
    }

    /// <summary>
    /// 2026-09-13-35a4 replaced this test's premise. It used to pass a parent stamp and
    /// assert the fall-through, because nothing published a rung yet; a stamped ticket now
    /// CREATES the rung it did not find (see <c>RungPublicationTests</c>). What "exactly as
    /// today" still means is a ticket that is not a slice at all — no stamp, no rung, no
    /// push, and a repository no slice touches never grows a branch.
    /// </summary>
    [Fact]
    public async Task WorkBranch_NoParentStamp_IsCutExactlyAsToday()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);

        var (checkout, sandbox) = await CheckOutAsync(
            fixture, TicketBranch, composedFromTicket: true, parentTicketId: null);

        checkout.Repository.Should().NotBeNull();
        sandbox.Ran("push").Should().BeFalse("a ticket that is not a slice has no feature to publish");
        sandbox.Ran("checkout", $"origin/{Rung}").Should()
            .BeFalse("a rung that does not exist is not checked out — the ladder falls through");
        sandbox.Ran("checkout", "-b", TicketBranch).Should().BeTrue();
        sandbox.Ran("merge").Should().BeFalse("a branch created from the base is already at the base");
    }

    private static async Task<string> GitAsync(ISandbox sandbox, params string[] args)
    {
        var result = await sandbox.RunStepAsync(
            new AgentSmith.Sandbox.Wire.Step(
                AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(),
                AgentSmith.Sandbox.Wire.StepKind.Run, Command: "git", Args: args,
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: 60),
            progress: null, CancellationToken.None);
        return result.OutputContent ?? string.Empty;
    }

    private static async Task<(RepoCheckout Checkout, RecordingSandbox Sandbox)> CheckOutAsync(
        GitRemoteFixture fixture, string branch, bool composedFromTicket,
        string? parentTicketId = null)
    {
        var config = new RepoConnection
        {
            Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath
        };
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(branch), config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);

        var sandbox = new RecordingSandbox(new InProcessSandbox(
            jobId: "p0496", workDir: fixture.WorkPath, ownsWorkDir: false, NullLogger.Instance));
        var cloner = new SandboxRepoCloner(
            factory.Object, TestGit.Identity, TestGit.WorkBranchCheckout,
            NullLogger<SandboxRepoCloner>.Instance);

        var checkout = await cloner.CheckoutIntoSandboxesAsync(
            config, new RunBranch(new BranchName(branch), composedFromTicket, parentTicketId),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
        return (checkout, sandbox);
    }
}
