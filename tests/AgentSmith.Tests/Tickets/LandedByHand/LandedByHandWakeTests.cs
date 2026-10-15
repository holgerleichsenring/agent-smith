using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Tickets.LandedByHand.LandedByHandFixture;

namespace AgentSmith.Tests.Tickets.LandedByHand;

/// <summary>
/// 2026-10-14-dbcei: a by-hand landing goes through the merge's one recorder — it wakes the tickets that waited
/// for it, a woken ticket whose approver could start runs is claimed, the gate admits it at once, and two marks
/// racing past the pre-check leave one landing and a 409 naming it.
/// </summary>
public sealed class LandedByHandWakeTests : IDisposable
{
    private readonly LandedByHandFixture _f = new();
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private ServiceProvider? _scopes;

    public void Dispose()
    {
        _scopes?.Dispose();
        _f.Dispose();
    }

    [Fact]
    public async Task LandedByHand_HeldDependentWhoseApproverCouldStartRuns_IsWokenAndClaimed()
    {
        await _f.HeldAsync("8", "7");

        Landed(await _f.MarkAsync());

        var nudge = _f.Nudged.Should().ContainSingle().Subject;
        (nudge.TicketId, nudge.Origin).Should().Be(("8", ReworkNudgeOrigin.RequirementsMet));
        (await Start().StartAsync(Project, new ClaimedReworkNudge("p", "8", nudge.Origin, null, null, 1, "tok", 0), default))
            .Should().Be(ReworkNudgeDisposition.Finish);
        _f.Tickets.Labels.Should().Equal(Tag);
        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once());
    }

    [Fact]
    public async Task LandedByHand_GateThenAdmitsTheDependent()
    {
        await _f.HeldAsync("8", "7");
        var gate = new RequiresUnmetGate(_f.Unmet);
        var claim = new ClaimRequest("GitHub", "p", new TicketId("8"), "code");
        (await gate.RefusalAsync(claim, _f.Config, default)).Should().NotBeNull("8 waits for 7");

        Landed(await _f.MarkAsync());

        (await gate.RefusalAsync(claim, _f.Config, default)).Should().BeNull("a by-hand landing meets the requirement");
    }

    [Fact]
    public async Task LandedByHand_TwoMarksRacing_OneLandingOne409NamingTheWinner()
    {
        var fired = false;
        LandedByHandFixture? f = null;
        IResult? other = null;
        using var racing = new LandedByHandFixture(db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            other = await f!.MarkAsync("the other mark", user: Person(("sub", "u-9"), ("name", "Grace")));
        }));
        f = racing;

        var mine = await racing.MarkAsync("mine");

        Landed(other!).By.Should().Be("Grace");
        Said(mine).Should().Be((StatusCodes.Status409Conflict,
            "7 is already marked done by Grace at 2026-10-13 10:00:00Z: the other mark."));
        racing.Rows().Should().ContainSingle().Which.Note.Should().Be("the other mark");
    }

    private RequirementsMetStart Start()
    {
        _scopes = new ServiceCollection().AddScoped(_ => FiledWorkDoubles.Move(_f.Config)).BuildServiceProvider();
        var fallback = new ReworkFallbackSpawn(_f.Loader.Object, new ServerContext("c.yml"), _f.Tickets,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), FiledWorkDoubles.Resolver(), _spawn.Object,
            NullLogger<ReworkFallbackSpawn>.Instance);
        return new RequirementsMetStart(_f.Requirements, _f.Landings, new ClosedTicketRepository(_f.Store.ScopeFactory), _f.Unmet, _f.Tickets,
            _scopes.GetRequiredService<IServiceScopeFactory>(), fallback, NullLogger<RequirementsMetStart>.Instance);
    }
}
