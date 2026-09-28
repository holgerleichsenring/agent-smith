using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-28-1da5d: what THIS FRAMEWORK did about a ticket, kept apart from what the board shows.
/// <para>
/// The record existed and a design turn could not reach it: the reader that composes runs is a
/// PAGE read addressed by a dialog id, and a turn's seeds carried no run record at all. The first
/// cut of the phase beside this one assumed otherwise.
/// </para>
/// </summary>
public sealed class TicketRunsToolTests
{
    [Fact]
    public void TicketRuns_TheAnswer_NamesThisFrameworkAsItsSource()
    {
        // "The run says it is done" and "the board says it is done" are not one claim, so the
        // answer carries which one it is rather than leaving a reader to assume.
        TicketRunsResult.None.Source.Should().Contain("this framework");
    }

    [Fact]
    public void TicketRuns_ATicketWithNoRuns_IsNotARefusal()
    {
        TicketRunsResult.None.Runs.Should().BeEmpty();
    }

    [Fact]
    public void TicketRunsTool_TheToolItOffers_TakesNoTicketArgument()
    {
        var tool = new TicketRunsToolHost(new Bound()).GetTools(null, null).Single();

        tool.Name.Should().Be("ticket_runs");
        tool.JsonSchema.ToString().Should().NotContain("ticket");
        // It points at the other tool rather than pretending to answer for the tracker too.
        tool.Description.Should().Contain("ticket_work");
    }

    [Fact]
    public void TicketRunsTool_ARunItRecorded_CarriesItsPhasesAndPullRequests()
    {
        var run = new TicketRun(
            "r-1", "sample", "success", DateTimeOffset.UtcNow, ["2026-09-24-d4e1"], ["https://pr/9335"]);

        new TicketRunsResult([run]).Runs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(run);
    }

    private sealed class Bound : IBoundTicketRuns
    {
        public Task<TicketRunsResult> ForAsync(CancellationToken cancellationToken) =>
            Task.FromResult(TicketRunsResult.None);
    }
}
