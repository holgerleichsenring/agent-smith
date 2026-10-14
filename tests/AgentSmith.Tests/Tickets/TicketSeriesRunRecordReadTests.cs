using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-14-9f80b: GET /api/tickets/{project}/{ticketId}/series for attempts that published no cut —
/// the specs, records and acceptance they recorded instead, over the real registration and store.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketSeriesRunRecordReadTests : IDisposable
{
    private const string R1 = "2026-10-01T09-00-00-0001";
    private const string R2 = "2026-10-01T10-00-00-0002";
    private const string A = "2026-10-01-1891a";
    private const string B = "2026-10-01-1891b";

    private const string SpecKeyed = """
        spec: 2026-10-01-1891a
        goal: The endpoint answers with the order
        repos:
          - api
        steps:
          - id: s1
            action: Add the endpoint
            path: api:src/A.cs
        done:
          - the build exits 0
          - the endpoint returns 200
        outcome:
          run: r1
        """;

    private const string BeforeTheRename = """
        phase: p19222a
        goal: The migration runs
        steps:
          - id: s1
            action: Write the migration
          - id: s2
            action: Run it
        done:
          - the migration applies
        # Account
        # - the migration applies: met (applied on a fresh database)
        """;

    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_ServesTheRunSpecRows_SourceRunRecord()
    {
        await _store.AddAsync(Run(R1), Step(R1), RecordedRow(R1, A, 1, "done"), RecordedRow(R1, B, 2, "failed"));

        var series = await ReadAsync();

        series.Source.Should().Be(TicketSeriesSources.RunRecord);
        series.Recorded.Should().BeTrue();
        series.Specs.Select(s => (s.SpecId, s.Ordinal, s.State)).Should().Equal((A, 1, "done"), (B, 2, "failed"));
        series.Specs.Should().AllSatisfy(s =>
        {
            s.FileStem.Should().BeNull();
            (s.ExecutedBy, s.ExecutedByAttempt).Should().Be((R1, 1));
        });
        (series.RunId, series.Attempt, series.At, series.SetKey, series.Gate).Should().Be((null, null, null, null, null));
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_SpecKeyedRecord_ServesStepsDoneListAndRepos()
    {
        await _store.AddAsync(Run(R1), Step(R1), RecordedRow(R1, A, 1, "done"), SpecRecord(R1, A, SpecKeyed));

        var spec = (await ReadAsync()).Specs.Single();

        spec.Goal.Should().Be("The endpoint answers with the order");
        spec.Repos.Should().Equal("api");
        spec.Steps.Single().Target.Should().Be("api:src/A.cs");
        spec.Done.Select(c => c.Text).Should().Equal("the build exits 0", "the endpoint returns 200");
        (spec.HasRecord, spec.RecordNote, spec.Yaml).Should().Be((true, null, SpecKeyed));
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_PhaseKeyedRecordWithCommentAccount_ServesStepsAndDoneList()
    {
        await _store.AddAsync(Run(R1), Step(R1), RecordedRow(R1, A, 1, "done"), SpecRecord(R1, A, BeforeTheRename));

        var spec = (await ReadAsync()).Specs.Single();

        spec.SpecId.Should().Be(A, "the id is the row's, not the record's");
        spec.Goal.Should().Be("The migration runs");
        spec.Steps.Select(s => s.Id).Should().Equal("s1", "s2");
        spec.Done.Select(c => c.Text).Should().Equal("the migration applies");
        spec.HasRecord.Should().BeTrue();
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_MalformedRecord_ServesTheGoalWithRecordUnreadable()
    {
        await _store.AddAsync(Run(R1), Step(R1), RecordedRow(R1, A, 1, "done"), RecordedRow(R1, B, 2, "done"),
            SpecRecord(R1, A, "spec: p1\ngoal: [never closed\n"), SpecRecord(R1, B, "goal: no id key\n"));

        var specs = (await ReadAsync()).Specs;

        specs.Should().AllSatisfy(s =>
        {
            s.Goal.Should().Be($"Recorded goal of {s.SpecId}");
            (s.HasRecord, s.RecordNote, s.Yaml).Should().Be((false, TicketSeriesRecordNotes.Unreadable, string.Empty));
            s.Steps.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_WithoutSpecRecord_ServesTheGoalOnly()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), RecordedRow(R1, A, 1, "failed"));

        var spec = (await ReadAsync()).Specs.Single();

        spec.Goal.Should().Be($"Recorded goal of {A}");
        (spec.HasRecord, spec.RecordNote).Should().Be((false, TicketSeriesRecordNotes.NoRecord));
        spec.Steps.Should().BeEmpty();
        spec.Done.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_InProgressOnAnEndedRun_IsFailed()
    {
        await _store.AddAsync(Run(R1, status: "cancelled"), Step(R1), RecordedRow(R1, A, 1, "in_progress"),
            Run(R2, status: "running"), Step(R2), RecordedRow(R2, B, 1, "in_progress"));

        var specs = (await ReadAsync()).Specs;

        specs.Select(s => (s.SpecId, s.State)).Should().Equal((A, "failed"), (B, "in_progress"));
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_SingleSpecRunWithoutRecord_TakesTheRunsCriteria()
    {
        var unmeasured = new UnmeasuredAccountView("web", "the build was red");
        await _store.AddAsync(WithAcceptance(Run(R1), Acceptance([unmeasured], ("it builds", "met"), ("docs updated", "unmet"))),
            Step(R1), RecordedRow(R1, A, 1, "done"));

        var series = await ReadAsync();

        var spec = series.Specs.Single();
        spec.Done.Select(c => (c.Text, c.Verdict)).Should().Equal(("it builds", "met"), ("docs updated", "unmet"));
        spec.Unmeasured.Should().Equal(unmeasured);
        spec.VerdictRecorded.Should().BeTrue();
        series.UnattributedCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_MultiSpecRunNoAcceptance_VerdictRecordedFalse()
    {
        await _store.AddAsync(Run(R1), Step(R1), RecordedRow(R1, A, 1, "done"), RecordedRow(R1, B, 2, "done"),
            SpecRecord(R1, A, BeforeTheRename), SpecRecord(R1, B, BeforeTheRename.Replace("p19222a", "p19222b")));

        var specs = (await ReadAsync()).Specs;

        specs.Should().HaveCount(2).And.AllSatisfy(s =>
        {
            s.Steps.Should().HaveCount(2);
            s.Done.Should().ContainSingle().Which.Verdict.Should().BeNull();
            s.VerdictRecorded.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_CriterionMatchingNoSpec_IsUnattributed()
    {
        var unmeasured = new UnmeasuredAccountView("api", "the diff would not run");
        var acceptance = Acceptance([unmeasured], ("the build exits 0", "met"), ("the migration applies", "unmet"), ("the docs say so", "met"));
        await _store.AddAsync(WithAcceptance(Run(R1), acceptance), Step(R1),
            RecordedRow(R1, A, 1, "done"), RecordedRow(R1, B, 2, "failed"),
            SpecRecord(R1, A, SpecKeyed), SpecRecord(R1, B, BeforeTheRename));

        var series = await ReadAsync();

        series.Specs[0].Done.Select(c => c.Verdict).Should().Equal("met", null);
        series.Specs[1].Done.Single().Verdict.Should().Be("unmet");
        series.Specs.Should().AllSatisfy(s => s.Unmeasured.Should().BeEmpty("a run with two specs cannot say whose account it was"));
        var unattributed = series.UnattributedCriteria.Should().ContainSingle().Subject;
        (unattributed.RunId, unattributed.Attempt).Should().Be((R1, 1));
        unattributed.Criteria.Select(c => (c.Text, c.Verdict)).Should().Equal(("the docs say so", "met"));
        unattributed.Unmeasured.Should().Equal(unmeasured);
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_AppliesOperatorJudgement()
    {
        await _store.AddAsync(WithAcceptance(Run(R1), Acceptance(("the build exits 0", "unmet"))), Step(R1),
            RecordedRow(R1, A, 1, "done"),
            new RunCriterionJudgement
            {
                RunId = R1, CriterionKey = CriterionKey.Of("the build exits 0"), CriterionText = "the build exits 0",
                MachineStatus = "unmet", HumanStatus = "met", Reason = "CI was green", Author = "carol", RecordedAt = T.AddHours(1),
            });

        var criterion = (await ReadAsync()).Specs.Single().Done.Single();

        criterion.Verdict.Should().Be("unmet");
        criterion.Overrule.Should().Be(new TicketCriterionOverrule("met", "CI was green", "carol", T.AddHours(1)));
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_SameOrdinalInTwoAttempts_ServesDistinctOrdinals()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), RecordedRow(R1, A, 1, "failed"),
            Run(R2), Step(R2), RecordedRow(R2, B, 1, "done"), RecordedRow(R2, A, 2, "done"));

        var specs = (await ReadAsync()).Specs;

        specs.Select(s => (s.SpecId, s.Ordinal, s.State)).Should().Equal((A, 1, "done"), (B, 2, "done"));
        (specs[0].ExecutedBy, specs[0].ExecutedByAttempt).Should().Be((R2, 2));
    }

    [Fact]
    public async Task TicketSeries_Read_RunWithoutCut_ScopedRead_CarriesTheRunIdAndAttempt()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), RecordedRow(R1, A, 1, "failed"),
            Run(R2), Step(R2), RecordedRow(R2, B, 1, "done"));

        var scoped = await ReadAsync(R2);
        var all = await ReadAsync();

        (scoped.RunId, scoped.Attempt, scoped.Source).Should().Be((R2, 2, TicketSeriesSources.RunRecord));
        scoped.Specs.Select(s => s.SpecId).Should().Equal(B);
        (all.RunId, all.Attempt).Should().Be((null, null));
        all.Specs.Select(s => s.SpecId).Should().Equal(A, B);
    }

    [Fact]
    public async Task TicketSeries_Read_HandbackOnlyCut_StaysRecordedWithoutSpecs()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), RecordedRow(R1, A, 1, "done"),
            Run(R2, status: "waiting_for_input"), Step(R2), Handback(R2, 4, "Refused"));

        var series = await ReadAsync();

        series.Recorded.Should().BeTrue();
        series.Source.Should().Be(TicketSeriesSources.Cut);
        series.Specs.Should().BeEmpty();
        series.Handback!.Case.Should().Be("Refused");
    }

    private async Task<TicketSeriesView> ReadAsync(string? runId = null) =>
        (await TicketSeriesEndpoints.SeriesAsync("alpha", "42", runId, _store.Series, CancellationToken.None))
        .Should().BeOfType<Ok<TicketSeriesView>>().Subject.Value!;
}
