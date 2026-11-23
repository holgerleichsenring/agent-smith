using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-23-31dca against real git: a stacked run cuts each repository from ITS entry's prerequisite branch, stops with a
/// sentence when that branch is not readable, and cuts a repository without an entry from its own base — never publishing
/// a prerequisite's branch.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class StackedCheckoutTests
{
    private const string TicketBranch = "agent-smith/19106";
    private const string Prerequisite = "agent-smith/41";

    [Fact]
    public async Task Checkout_TheRepositorysEntry_IsCutFromThatPrerequisitesBranch()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null, rung: Prerequisite);

        var checkout = await CheckoutAsync(fixture, new Dictionary<string, string> { ["server"] = "41" });

        checkout.Problem.Should().BeNull();
        checkout.Rung.Should().Be(Prerequisite);
    }

    [Fact]
    public async Task Checkout_TheEntrysBranchIsNotReadable_StopsNamingIt_AndPublishesNothing()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);

        var checkout = await CheckoutAsync(fixture, new Dictionary<string, string> { ["server"] = "41" });

        checkout.Problem.Should().Contain($"'{Prerequisite}' is the base of server").And.Contain("#41");
        fixture.AskRemote("rev-parse", "--verify", Prerequisite).Exit.Should().NotBe(0, "a prerequisite's branch is never published");
    }

    [Fact]
    public async Task Checkout_NoEntryForTheRepository_CutsFromItsOwnBase()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null, rung: Prerequisite);

        var checkout = await CheckoutAsync(fixture, new Dictionary<string, string> { ["worker"] = "41" });

        checkout.Problem.Should().BeNull();
        checkout.Rung.Should().BeNull("a fall-through to the clone's own base is no chosen pull-request target");
    }

    // A parked run stamped before 2026-11-23-31dca names one id for every repo; a repo that prerequisite never touched falls
    // through to its own base, as it did when it was stamped — and nothing is published.
    [Fact]
    public async Task Checkout_TheOldSingleIdStamp_FallsThroughWhereItsBranchIsMissing()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);

        var checkout = await CheckoutAsync(fixture, new Dictionary<string, string>(), everyRepo: "41");

        checkout.Problem.Should().BeNull();
        checkout.Rung.Should().BeNull();
        fixture.AskRemote("rev-parse", "--verify", Prerequisite).Exit.Should().NotBe(0);
    }

    private static async Task<RepoCheckout> CheckoutAsync(
        GitRemoteFixture fixture, IReadOnlyDictionary<string, string> stacked, string? everyRepo = null)
    {
        await using var sandbox = new InProcessSandbox(
            jobId: "31dca", workDir: fixture.WorkPath, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        return await RungPublicationTests.Cloner().CheckoutIntoSandboxesAsync(config,
            new RunBranch(new BranchName(TicketBranch), ComposedFromTicket: true, Stacked: new StackedBases(stacked, everyRepo)),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
    }
}
