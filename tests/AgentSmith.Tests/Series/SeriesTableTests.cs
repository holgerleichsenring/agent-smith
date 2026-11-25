using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using A = AgentSmith.Application.Services.Holds.TicketHoldAddressees;
using C = AgentSmith.Application.Services.Holds.TicketHoldCases;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: a ticket's facts read as the table's words, and a snapshot tells a change. 2026-11-25-9992a: the
/// words and kinds are the one state's — read from the hold the ticket list reads.</summary>
public sealed class SeriesTableTests
{
    [Fact]
    public void SeriesTable_States_ReadFromTheOneState()
    {
        var facts = new[]
        {
            SeriesRecords.Fact("43", RunStatuses.Success, landed: SeriesRecords.T.AddMinutes(18)),
            SeriesRecords.Fact("44", RunStatuses.Running, finished: false).Held(A.Nobody, C.Working),
            SeriesRecords.Fact("45", RunStatuses.Failed).Held(A.Decide, C.Failed) with { EndDetail = "a test expects the old schema" },
            SeriesRecords.Fact("46", requires: "44").Held(A.Nobody, C.Prerequisite, null, "SCRUM-44"),
            SeriesRecords.Fact("47"),
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T.AddMinutes(30));

        rows.Select(r => (r.State, r.Kind)).Should().Equal(
            ("merged", SeriesTicketKinds.Merged), ("coding", SeriesTicketKinds.Running),
            ("red · a test expects the old schema", SeriesTicketKinds.Failed), ("waits on SCRUM-44", SeriesTicketKinds.Waiting),
            ("not started", SeriesTicketKinds.Waiting));
        rows[0].Time.Should().Be("18m");
        rows[0].Cost.Should().Be("$1.5");
        rows[3].Cost.Should().Be("–");
    }

    [Fact]
    public void SeriesTable_OpenPullRequest_ReadsItsCiByTheHoldsCase()
    {
        var since = SeriesRecords.T.AddHours(-8);
        var facts = new[]
        {
            SeriesRecords.Fact("43", RunStatuses.Success).WithPr(PrReviewStates.Open, CiStates.Red, "build: CS0246").Held(A.Decide, C.CiFailed),
            SeriesRecords.Fact("44", RunStatuses.Success).WithPr(PrReviewStates.Open, CiStates.Pending).Held(A.Nobody, C.CiRunning, since),
        };

        var rows = SeriesTable.Rows(facts, SeriesRecords.T);

        rows.Select(r => (r.State, r.Kind)).Should().Equal(
            ("CI red · build: CS0246", SeriesTicketKinds.Failed), ("CI running since 17 Nov 10:37 UTC", SeriesTicketKinds.Pr));
        facts.Select(f => f.Ci).Should().Equal(CiStates.Red, CiStates.Pending);
    }

    [Fact]
    public void SeriesTicketFact_ClosedPullRequestsRedHead_IsNotTheTicketsCi()
    {
        // 2026-11-25-9992a: the master read every record's CI, so a closed pull request's red head read 'CI red'.
        var fact = SeriesRecords.Fact("43", RunStatuses.Success)
            .WithPr(PrReviewStates.Closed, CiStates.Red, "old: failed").WithPr(PrReviewStates.Open, CiStates.Green);

        (fact.Ci, fact.CiFailure, fact.Kind).Should().Be((CiStates.Green, null, SeriesTicketKinds.Pr));
    }

    [Fact]
    public void SeriesTable_InterruptedAttempt_ReadsRestarting_WhileItsRestartIsToCome_ElseRed()
    {
        var facts = new[]
        {
            SeriesRecords.Fact("43", "cancelled").Held(A.Nobody, C.Restarting),
            SeriesRecords.Fact("44", "cancelled").Held(A.Decide, C.Interrupted),
        };

        SeriesTable.Rows(facts, SeriesRecords.T).Select(r => (r.State, r.Kind)).Should().Equal(
            ("interrupted · restarting", SeriesTicketKinds.Running),
            ("red · interrupted, not restarted by itself", SeriesTicketKinds.Failed));
    }

    [Fact]
    public void SeriesTable_HeldTicketWhoseMergesRemain_NamesThem()
    {
        var facts = new[]
        {
            SeriesRecords.Fact("46", requires: "44").Held(A.Nobody, C.StartsOnMerge, null, "SCRUM-44", "#99"),
            SeriesRecords.Fact("47", requires: "44").Held(A.Nobody, C.StartsOnMerge, null, "SCRUM-44"),
        };

        SeriesTable.Rows(facts, SeriesRecords.T).Select(r => r.State).Should().Equal(
            "starts once SCRUM-44, #99 are merged", "starts once SCRUM-44 is merged");
    }

    [Fact]
    public void SeriesJudgement_NewlyRed_OnlyWhatTurnedRedSinceTheLastTable()
    {
        var record = SeriesRecords.Running() with { LastTable = "SCRUM-43=failed|red · failed\nSCRUM-44=running|coding" };
        var rows = SeriesTable.Rows(
            [SeriesRecords.Fact("43", RunStatuses.Failed), SeriesRecords.Fact("44", RunStatuses.Failed)], SeriesRecords.T);

        SeriesJudgement.NewlyRed(record, rows).Should().ContainSingle().Which.Should().StartWith("SCRUM-44 turned red");
    }

    [Fact]
    public void SeriesJudgement_NewlyRed_ASnapshotStoredBeforeKinds_FlagsNothing()
    {
        // 2026-11-25-9992a: the words changed with the one state, not the tickets — a deploy re-reports no red ticket.
        var record = SeriesRecords.Running() with { LastTable = "SCRUM-43=red · failed\nSCRUM-44=coding" };
        var rows = SeriesTable.Rows(
            [SeriesRecords.Fact("43", RunStatuses.Failed), SeriesRecords.Fact("44", RunStatuses.Failed)], SeriesRecords.T);

        SeriesJudgement.NewlyRed(record, rows).Should().BeEmpty();
        SeriesTable.Parse(record.LastTable)["SCRUM-43"].Should().Be(((string?)null, "red · failed"));
    }

    [Fact]
    public void SeriesBeatDue_TableChanged_IsDue_UnchangedIsNot()
    {
        var facts = new[] { SeriesRecords.Fact("43", RunStatuses.Running, finished: false) };
        var record = SeriesRecords.Running();

        SeriesBeatDue.Why(record, facts, SeriesRecords.T).Should().NotBeNull("the first beat has seen nothing");
        var seen = record with { LastTable = SeriesTable.Snapshot(SeriesTable.Rows(facts, SeriesRecords.T)), LastStatusAt = SeriesRecords.T };
        SeriesBeatDue.Why(seen, facts, SeriesRecords.T.AddMinutes(14)).Should().BeNull();
        SeriesBeatDue.Why(seen, facts, SeriesRecords.T.AddDays(2)).Should().BeNull("2026-11-25-96aec: no beat runs on time alone");
    }

    // 2026-11-25-9992e: a Delivering series has no CI-poll beat of its own; 2026-11-25-96aec: nor a cadence beat.
    [Fact]
    public void SeriesBeatDue_Delivering_HasNoBeatOfItsOwn()
    {
        var facts = new[] { SeriesRecords.Fact("43", landed: SeriesRecords.T) };
        var record = SeriesRecords.Running() with
        {
            State = AgentSmith.Contracts.Series.SeriesRunStates.Delivering, LastTable = SeriesTable.Snapshot(SeriesTable.Rows(facts, SeriesRecords.T)),
            LastStatusAt = SeriesRecords.T,
        };

        SeriesBeatDue.Why(record, facts, SeriesRecords.T.AddSeconds(60)).Should().BeNull();
        SeriesBeatDue.Why(record, facts, SeriesRecords.T.AddMinutes(15)).Should().BeNull();
    }
}
