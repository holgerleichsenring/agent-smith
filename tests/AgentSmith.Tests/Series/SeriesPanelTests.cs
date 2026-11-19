using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207e: the panel counts what landed, follows the longest unlanded requires chain, estimates the
/// finish only once two tickets landed, and heads itself with a waiting question.</summary>
public sealed class SeriesPanelTests
{
    private static readonly ResolvedProject Project = new() { Name = "sample" };

    private static SeriesTicketFact[] Facts(int landed)
    {
        var land = (string id, int i) => SeriesRecords.Fact(id, RunStatuses.Success, landed: i < landed ? SeriesRecords.T.AddMinutes(30) : null);
        return [land("43", 0), land("44", 1) with { Requires = ["43"] }, SeriesRecords.Fact("45", RunStatuses.Running, finished: false, requires: "44"),
            SeriesRecords.Fact("46", requires: "45"), SeriesRecords.Fact("47", requires: "43")];
    }

    [Fact]
    public void Panel_TwoLanded_CountsFollowsTheLongestChainAndEstimates()
    {
        var view = SeriesPanel.Of(SeriesRecords.Running(), Facts(2), SeriesRecords.T.AddHours(1), Project, new DialogueGlobalConfig());

        view.Landed.Should().Be(2);
        view.CriticalPath.Should().Equal("SCRUM-45", "SCRUM-46");
        view.CriticalPathLeft.Should().Be("2 tickets, about 1 h 00 min left");
        view.Eta.Should().Be("20:37 UTC");
        view.Tickets.Select(n => n.Ticket).Should().Equal(["SCRUM-43", "SCRUM-44", "SCRUM-45", "SCRUM-46", "SCRUM-47"],
            "2026-11-19-1b72a: the pane lists every ticket");
        view.HeadState.Should().Be("series running");
    }

    [Fact]
    public void Panel_OneLanded_HasNoEstimate_AndAWaitingQuestionShowsInTheHead()
    {
        var record = SeriesRecords.Running() with { Question = SeriesQuestions.Ask("Keep it?", ["yes"], 0, SeriesRecords.T, 120) };

        var view = SeriesPanel.Of(record, Facts(1), SeriesRecords.T.AddHours(1), Project, new DialogueGlobalConfig());

        view.Eta.Should().BeNull();
        view.HeadState.Should().Be("series running · 1 question for you");
        view.Question.Should().Be("Keep it?");
    }

    [Fact]
    public void Panel_PausedSeries_HeadsItselfPaused_AndListsEveryTicketWithItsState()
    {
        var record = SeriesRecords.Running() with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Running };

        var view = SeriesPanel.Of(record, Facts(1), SeriesRecords.T.AddHours(1), Project, new DialogueGlobalConfig());

        view.HeadState.Should().Be("series paused");
        view.Tickets.Should().HaveCount(5);
        view.Tickets[2].Should().Match<SeriesTableRow>(r => r.Ticket == "SCRUM-45" && r.State == "coding" && r.Cost == "$1.5");
    }
}
