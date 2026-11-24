using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-24-f9c5b against real git: a ticket of a series with a series branch publishes that branch at the base when it
/// is the first there, adopts it when a sibling did, cuts its own branch from it, and opens its pull request against it.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SeriesBranchCutTests
{
    private const string Series = "agent-smith/series/sr-1";
    private const string TicketBranch = "agent-smith/19106";

    [Fact]
    public async Task Checkout_FirstTicketOfTheSeries_PublishesTheBranchAtTheBaseAndTargetsIt()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);

        var pipeline = await CheckOutAsync(fixture);

        Sha(fixture, Series).Should().Be(Sha(fixture, GitRemoteFixture.BaseBranch), "the series branch is cut from the default branch");
        PullRequestTargets.For(pipeline, "server")!.Value.Should().Be(Series);
    }

    [Fact]
    public async Task Checkout_ALaterTicket_IsCutFromTheSeriesBranchWithItsPredecessorsWork()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null, rung: Series);
        fixture.AskRemote("checkout", GitRemoteFixture.BaseBranch);
        fixture.AdvanceBase();

        await CheckOutAsync(fixture);

        fixture.WorkFileExists(GitRemoteFixture.RungOnlyFile).Should().BeTrue("a merged predecessor's work is on the series branch");
        fixture.WorkFileExists(GitRemoteFixture.BaseOnlyFile).Should()
            .BeFalse("the default branch moving meanwhile is not taken in until the final pull request");
        Sha(fixture, Series).Should().NotBe(Sha(fixture, GitRemoteFixture.BaseBranch), "an existing series branch is adopted, never reset");
    }

    private static async Task<PipelineContext> CheckOutAsync(GitRemoteFixture fixture)
    {
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        await using var sandbox = new InProcessSandbox(
            jobId: "f9c5b", workDir: fixture.WorkPath, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [config]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["server"] = sandbox });
        var handler = new CheckoutSourceHandler(RungPublicationTests.Cloner(), RunStateConceptsTestFactory.Default, new SandboxTargets(),
            NullLogger<CheckoutSourceHandler>.Instance);

        var result = await handler.ExecuteAsync(new CheckoutSourceContext([config],
            new RunBranch(new BranchName(TicketBranch), ComposedFromTicket: true, SeriesBranch: Series), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        return pipeline;
    }

    private static string Sha(GitRemoteFixture fixture, string rev) => fixture.AskRemote("rev-parse", rev).Output.Trim();
}
