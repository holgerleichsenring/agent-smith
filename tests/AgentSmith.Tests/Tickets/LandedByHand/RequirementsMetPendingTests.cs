using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Tickets.LandedByHand.LandedByHandFixture;

namespace AgentSmith.Tests.Tickets.LandedByHand;

/// <summary>
/// 2026-11-22-d434a: measured — a prerequisite turned green, a new attempt of it started seconds later, and the
/// dependents' wake saw that unfinished attempt, found the requirement unmet and was spent for good: a requirement a
/// green branch lifts gets no landing to wake it again. While a prerequisite's newest attempt is live the wake
/// reschedules; once it ended red the wake is spent as before.
/// </summary>
public sealed class RequirementsMetPendingTests : IDisposable
{
    private readonly LandedByHandFixture _f = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private ServiceProvider? _scopes;

    public void Dispose()
    {
        _scopes?.Dispose();
        _f.Dispose();
    }

    [Fact]
    public async Task StartAsync_PrerequisitesNewestAttemptStillRuns_Reschedules()
    {
        await _f.HeldAsync("8", "7");
        Prerequisite(new PreviousAttempt("run-7b", RunStatuses.Running, T, Finished: false));

        (await Start().StartAsync(Project, Nudge(), default)).Should().Be(ReworkNudgeDisposition.Reschedule);
    }

    [Fact]
    public async Task StartAsync_PrerequisitesNewestAttemptEndedRed_IsSpent()
    {
        await _f.HeldAsync("8", "7");
        Prerequisite(new PreviousAttempt("run-7b", RunStatuses.Failed, T, Finished: true));

        (await Start().StartAsync(Project, Nudge(), default)).Should().Be(ReworkNudgeDisposition.Finish);
    }

    [Fact]
    public async Task StartAsync_PrerequisiteParkedOnAQuestion_IsSpent()
    {
        await _f.HeldAsync("8", "7");
        Prerequisite(new PreviousAttempt("run-7b", RunStatuses.WaitingForInput, T, Finished: false));

        (await Start().StartAsync(Project, Nudge(), default)).Should().Be(ReworkNudgeDisposition.Finish,
            "nothing but a person ends a parked run, so the wake does not wait on it");
    }

    private void Prerequisite(PreviousAttempt attempt) =>
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>())).ReturnsAsync(attempt);

    private static ClaimedReworkNudge Nudge() => new("p", "8", ReworkNudgeOrigin.RequirementsMet, null, null, 1, "tok", 0);

    private RequirementsMetStart Start()
    {
        _scopes = new ServiceCollection().AddScoped(_ => ActSteps.Start(_f.Config, FiledWorkDoubles.Move(_f.Config), _f.Tickets)).BuildServiceProvider();
        var fallback = new ReworkFallbackSpawn(_f.Loader.Object, new ServerContext("c.yml"), _f.Tickets,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), FiledWorkDoubles.Resolver(), Mock.Of<ISpawnPipelineRunsUseCase>(),
            NullLogger<ReworkFallbackSpawn>.Instance);
        return new RequirementsMetStart(new WaitingTicketStart(_f.Requirements,
            new TicketStartHold(_f.Landings, new ClosedTicketRepository(_f.Store.ScopeFactory), _f.Tickets, NullLogger<TicketStartHold>.Instance),
            _f.Unmet, _scopes.GetRequiredService<IServiceScopeFactory>(), fallback, _attempts.Object, Mock.Of<IActiveRunLease>(),
            NullLogger<WaitingTicketStart>.Instance));
    }
}
