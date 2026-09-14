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
/// 2026-09-13-35a4 against real git: the feature's branch is created once, by whichever
/// slice reaches the repository first, and a slice that loses that race ADOPTS the winner's
/// branch instead of overwriting it.
/// <para>
/// Asked of git rather than of an argument list, because the whole question is what the
/// remote does with a push. A recorded refspec cannot tell a create from an overwrite, and
/// overwriting is exactly the failure this phase exists to make impossible: the work-branch
/// pusher force-pushes with a lease and refreshes that lease with a fetch, which on a
/// shared branch would delete every slice already merged into it.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class RungPublicationTests
{
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";
    private const string TicketBranch = "agent-smith/19106";

    [Fact]
    public async Task RungPublish_Absent_CreatesItAtTheNextRungSha()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        await using var sandbox = Clone(fixture, "slice-one");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo(fixture), ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(Rung);
        resolved.FellThrough.Should().BeFalse("the rung exists now, because this slice made it");
        Sha(fixture, Rung).Should().Be(Sha(fixture, GitRemoteFixture.BaseBranch),
            "the rung is created at the sha of the ladder's next rung down");
    }

    [Fact]
    public async Task RungPublish_RejectedNonFastForward_AdoptsAndSucceeds()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        // Both clones are taken BEFORE the rung exists — the window the race lives in.
        await using var first = Clone(fixture, "slice-one");
        await using var second = Clone(fixture, "slice-two");

        await TestGit.RungPublisher.EnsureAsync(first, Repo(fixture), ParentTicket, CancellationToken.None);
        // What a rung is FOR: the first slice's pull request has been merged into it.
        fixture.AdvanceRung(Rung);

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            second, Repo(fixture), ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(Rung, "a rejection means somebody else created it, not that this run failed");
        resolved.FellThrough.Should().BeFalse();
        Files(fixture, Rung).Should().Contain(GitRemoteFixture.RungLaterFile,
            "the losing slice must never force the rung back to the base — that would delete "
            + "every slice already merged into it");
        Sha(fixture, Rung).Should().NotBe(Sha(fixture, GitRemoteFixture.BaseBranch));
    }

    [Fact]
    public async Task RungPublish_TwoConcurrentSlices_OneCreatesTheOtherAdopts()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        await using var first = Clone(fixture, "slice-one");
        await using var second = Clone(fixture, "slice-two");
        var publisher = TestGit.RungPublisher;

        // Two slices with no predecessor edge between them start together by design: the
        // ordering gate holds only DECLARED predecessors.
        var both = await Task.WhenAll(
            publisher.EnsureAsync(first, Repo(fixture), ParentTicket, CancellationToken.None),
            publisher.EnsureAsync(second, Repo(fixture), ParentTicket, CancellationToken.None));

        both.Should().OnlyContain(r => r.Name == Rung && !r.FellThrough,
            "exactly one can win, and the other's correct behaviour is to take what is there");
        Branches(fixture).Should().BeEquivalentTo([GitRemoteFixture.BaseBranch, Rung],
            "one feature, one branch — publishing is idempotent");
        Sha(fixture, Rung).Should().Be(Sha(fixture, GitRemoteFixture.BaseBranch));
    }

    [Fact]
    public async Task WorkBranch_IsCutAfterTheRungExists()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        var config = Repo(fixture);
        var sandbox = new RecordingSandbox(new InProcessSandbox(
            jobId: "35a4", workDir: fixture.WorkPath, ownsWorkDir: false, NullLogger.Instance));

        var checkout = await Cloner().CheckoutIntoSandboxesAsync(
            config, new RunBranch(new BranchName(TicketBranch), ComposedFromTicket: true, ParentTicket),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);

        checkout.Repository.Should().NotBeNull();
        var published = IndexOf(sandbox, "push");
        var cutFrom = IndexOf(sandbox, $"origin/{Rung}");
        var created = IndexOf(sandbox, "-b");
        published.Should().BeGreaterThanOrEqualTo(0, "the rung does not exist, so this run publishes it");
        cutFrom.Should().BeGreaterThan(published,
            "2026-09-13-5cdf resolves the rung by asking whether it EXISTS — publishing after "
            + "the cut would leave the first slice on a branch it is not on");
        created.Should().BeGreaterThan(cutFrom);
        Sha(fixture, Rung).Should().Be(Sha(fixture, GitRemoteFixture.BaseBranch));
    }

    private static SandboxRepoCloner Cloner()
    {
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(TicketBranch), "unused"));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return new SandboxRepoCloner(
            factory.Object, TestGit.Identity, TestGit.WorkBranchCheckout,
            NullLogger<SandboxRepoCloner>.Instance);
    }

    private static int IndexOf(RecordingSandbox sandbox, string arg) =>
        sandbox.Steps.FindIndex(s => s.Command == "git" && s.Args?.Contains(arg) == true);

    private static InProcessSandbox Clone(GitRemoteFixture fixture, string name) =>
        new(jobId: name, workDir: fixture.NewClone(name), ownsWorkDir: false, NullLogger.Instance);

    private static RepoConnection Repo(GitRemoteFixture fixture) =>
        new() { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };

    private static string Sha(GitRemoteFixture fixture, string rev) =>
        fixture.AskRemote("rev-parse", rev).Output.Trim();

    private static IEnumerable<string> Files(GitRemoteFixture fixture, string rev) =>
        fixture.AskRemote("ls-tree", "--name-only", rev).Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> Branches(GitRemoteFixture fixture) =>
        fixture.AskRemote("for-each-ref", "--format=%(refname:short)", "refs/heads/").Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
