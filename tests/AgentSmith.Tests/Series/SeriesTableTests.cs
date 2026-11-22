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
    public void SeriesTable_GreenRun_ReadsItsCiFromTheCiLoopsRecords()
    {
        var facts = new[]
        {
            SeriesRecords.Fact("43", RunStatuses.Success) with { CiState = "red", CiFailure = "build: CS0246" },
            SeriesRecords.Fact("44", RunStatuses.Success) with { CiState = "pending" },
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T);

        rows.Select(r => r.State).Should().Equal("CI red · build: CS0246", "green · CI running");
        rows[0].Kind.Should().Be(SeriesTicketState.Red, "a red CI is news the master hears");
    }

    [Fact]
    public void SeriesTable_InterruptedAttempt_ReadsRestarting_WhileItsRestartIsToCome_ElseRed()
    {
        // 2026-11-22-6d85c: the server stopped under it; the interrupted restart retries it once.
        var facts = new[]
        {
            SeriesRecords.Fact("43", "cancelled") with { EndDetail = "interrupted", Interrupted = true, Restarting = true },
            SeriesRecords.Fact("44", "cancelled") with { EndDetail = "interrupted", Interrupted = true },
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T);

        rows.Select(r => (r.State, r.Kind)).Should().Equal(
            ("interrupted · restarting", SeriesTicketState.Running),
            ("red · interrupted, not restarted by itself", SeriesTicketState.Red));
    }

    [Fact]
    public void SeriesTable_HeldTicketWhoseMergesRemain_NamesThem()
    {
        // 2026-11-22-6d85d: a member by its key, a prerequisite outside the series by its id.
        var facts = new[]
        {
            SeriesRecords.Fact("44", RunStatuses.Success),
            SeriesRecords.Fact("46", requires: "44") with { StartsOnceMerged = ["44", "99"] },
            SeriesRecords.Fact("47", requires: "44") with { StartsOnceMerged = ["44"] },
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T);

        rows.Skip(1).Select(r => (r.State, r.Kind)).Should().Equal(
            ("starts once SCRUM-44 and #99 are merged", SeriesTicketState.Held),
            ("starts once SCRUM-44 is merged", SeriesTicketState.Held));
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

        SeriesBeatDue.Why(record, facts, SeriesRecords.T, 15).Should().NotBeNull("the first beat has seen nothing");
        var seen = record with { LastTable = SeriesTable.Snapshot(SeriesTable.Rows(facts, SeriesRecords.T)), LastStatusAt = SeriesRecords.T };
        SeriesBeatDue.Why(seen, facts, SeriesRecords.T.AddMinutes(14), 15).Should().BeNull();
        SeriesBeatDue.Why(seen, facts, SeriesRecords.T.AddMinutes(15), 15).Should().Be("the status is due");
    }
}
