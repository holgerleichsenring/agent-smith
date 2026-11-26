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
}
