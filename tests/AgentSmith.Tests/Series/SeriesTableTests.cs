using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: a ticket's facts read as the table's words, and a snapshot tells a change.</summary>
public sealed class SeriesTableTests
{
    [Fact]
    public void SeriesTable_States_ReadFromTheFacts()
    {
        var facts = new[]
        {
            SeriesRecords.Fact("43", RunStatuses.Success, landed: SeriesRecords.T.AddMinutes(18)),
            SeriesRecords.Fact("44", RunStatuses.Running, finished: false),
            SeriesRecords.Fact("45", RunStatuses.Failed) with { EndDetail = "a test expects the old schema" },
            SeriesRecords.Fact("46", requires: "44"),
            SeriesRecords.Fact("47"),
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T.AddMinutes(30));

        rows.Select(r => r.State).Should().Equal(
            "landed", "coding", "red · a test expects the old schema", "waits on SCRUM-44", "not started");
        rows[0].Time.Should().Be("18m");
        rows[0].Cost.Should().Be("$1.5");
        rows[3].Cost.Should().Be("–");
    }

    [Fact]
    public void SeriesJudgement_NewlyRed_OnlyWhatTurnedRedSinceTheLastTable()
    {
        var record = SeriesRecords.Running() with { LastTable = "SCRUM-43=red · failed\nSCRUM-44=coding" };
        var rows = SeriesTable.Rows(
            [SeriesRecords.Fact("43", RunStatuses.Failed), SeriesRecords.Fact("44", RunStatuses.Failed)], SeriesRecords.T);

        SeriesJudgement.NewlyRed(record, rows).Should().ContainSingle().Which.Should().StartWith("SCRUM-44 turned red");
    }

    [Fact]
    public void SeriesBeatDue_TableChanged_IsDue_UnchangedIsNot()
    {
        var facts = new[] { SeriesRecords.Fact("43", RunStatuses.Running, finished: false) };
        var record = SeriesRecords.Running();

        SeriesBeatDue.Why(record, facts, SeriesRecords.T).Should().NotBeNull("the first beat has seen nothing");
        var seen = record with { LastTable = SeriesTable.Snapshot(SeriesTable.Rows(facts, SeriesRecords.T)) };
        SeriesBeatDue.Why(seen, facts, SeriesRecords.T).Should().BeNull();
    }
}
