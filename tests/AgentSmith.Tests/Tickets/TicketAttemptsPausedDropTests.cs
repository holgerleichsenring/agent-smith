using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-11-19-1b72a: a start the capacity pump let go while the ticket's series was paused never began, so it is
/// no attempt — the real first attempt after the resume stays attempt #1.</summary>
public sealed class TicketAttemptsPausedDropTests
{
    [Fact]
    public void TicketAttempts_StartHeldByAPausedSeries_IsNotNumbered()
    {
        var held = TicketStoreHarness.Run("r-1", status: TicketAttempts.Cancelled);
        held.EndKind = RunEndKinds.SeriesPaused;
        var attempts = new TicketAttempts();

        var ordered = attempts.Of([new TicketRunRow(held, HasStep: false), new TicketRunRow(TicketStoreHarness.Run("r-2", status: "failed"), HasStep: true)]);

        ordered.Select(a => a.Run.Id).Should().Equal("r-2");
    }
}
