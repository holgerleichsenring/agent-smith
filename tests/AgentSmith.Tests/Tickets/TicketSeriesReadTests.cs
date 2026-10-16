using AgentSmith.Contracts.Commands;
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
/// 2026-10-13-9a59e: GET /api/tickets/{project}/{ticketId}/series — the cut an attempt published, each
/// spec's standing and verdicts, the gate, and the revisions, over the real registration and store.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketSeriesReadTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketSeries_Read_HandbackAfterCut_KeepsTheSpecs()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)),
            Run(R2, status: "waiting_for_input"), Step(R2), Handback(R2, 4, "Refused"));

        var series = await ReadAsync();

        series.RunId.Should().Be(R1);
        series.Specs.Select(s => s.SpecId).Should().Equal(A, B);
        series.Specs[0].Goal.Should().Be($"Goal of {A}");
        series.Specs[0].Repos.Should().Equal("api");
        series.Specs[0].Steps.Single().Target.Should().Be("api:src/A.cs");
        series.Handback.Should().BeEquivalentTo(new TicketSeriesHandbackView("Refused", R2, 2, T.AddMinutes(4)));
    }

    [Fact]
    public async Task TicketSeries_Read_WithRunId_ReturnsThatAttemptsCutHandbackCaseAndGate()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1)), Gate(R1, "success"),
            Run(R2, status: "waiting_for_input"), Step(R2), Handback(R2, 4, "Question"));

        var older = await ReadAsync(R1);
        var newer = await ReadAsync(R2);

        older.Specs.Should().ContainSingle();
        older.Handback.Should().BeNull();
        older.Gate.Should().Be(new TicketSeriesGateView("passed", 1, R1));
        newer.Specs.Should().BeEmpty();
        newer.Handback!.Case.Should().Be("Question");
        newer.Gate.Should().Be(new TicketSeriesGateView("skipped", 0, R2));
    }

    [Fact]
    public async Task TicketSeries_Read_CarriedSpecNamesTheAttemptThatExecutedIt()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)),
            SpecRow(R1, A, "done", Acceptance(("it builds", "met"))), SpecRow(R1, B, "failed"),
            Run(R2), Step(R2), Cut(R2, 5, Entry(A, 1), Entry(B, 2)), Attempt(R2, 2, "ticket edited", A),
            SpecRow(R2, B, "in_progress"));

        var specs = (await ReadAsync()).Specs;

        specs[0].State.Should().Be("carried");
        (specs[0].ExecutedBy, specs[0].ExecutedByAttempt).Should().Be((R1, 1));
        specs[1].State.Should().Be("in_progress");
        (specs[1].ExecutedBy, specs[1].ExecutedByAttempt).Should().Be((R2, 2));
    }

    [Fact]
    public async Task TicketSeries_Read_AppliesOperatorJudgementByCriterionText()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1, "the  build exits 0", "docs updated")),
            SpecRow(R1, A, "done", Acceptance(("the build exits 0", "unmet"))),
            new RunCriterionJudgement
            {
                RunId = R1, CriterionKey = AgentSmith.Contracts.Runs.CriterionKey.Of("the build exits 0"),
                CriterionText = "the build exits 0", MachineStatus = "unmet", HumanStatus = "met",
                Reason = "CI was green", Author = "carol", RecordedAt = T.AddHours(1),
            });

        var done = (await ReadAsync()).Specs.Single().Done;

        done[0].Verdict.Should().Be("unmet");
        done[0].Overrule.Should().Be(new TicketCriterionOverrule("met", "CI was green", "carol", T.AddHours(1)));
        done[1].Verdict.Should().BeNull("nothing judged it");
        done[1].Overrule.Should().BeNull();
    }

    [Fact]
    public async Task TicketSeriesSpecComposer_EndedRow_IsServedAsJudgedBy()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1, "the build exits 0")),
            SpecRow(R1, A, "failed", Acceptance(("the build exits 0", "unmet"))));

        (await ReadAsync()).Specs.Single().JudgedBy.Should().Be(R1); // 2026-10-16-86e6e
    }

    [Fact]
    public async Task TicketSeries_Read_GatePassedWithTheCutsSpecCount()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)), Gate(R1, "success"));

        (await ReadAsync()).Gate.Should().Be(new TicketSeriesGateView("passed", 2, R1));
    }

    [Fact]
    public async Task TicketSeries_Read_RevisionsComeFromEachAttemptsRecord()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1)), Attempt(R1, 1, "initial"),
            Run(R2), Step(R2), Cut(R2, 7, Entry(A, 1)), Attempt(R2, 2, "comment on ticket"));

        (await ReadAsync()).Revisions.Should().Equal(
            new TicketSeriesRevisionView(1, "initial", R1, 1, T.AddMinutes(3)),
            new TicketSeriesRevisionView(2, "comment on ticket", R2, 2, T.AddMinutes(7)));
    }

    [Fact]
    public async Task TicketSeries_Read_NoRunSpecRows_IsEmpty()
    {
        await _store.AddAsync(Run(R1), Step(R1));

        var series = await ReadAsync();

        series.Recorded.Should().BeFalse("the run published no cut and recorded no spec row");
        series.Specs.Should().BeEmpty();
        series.RunId.Should().BeNull();
    }

    [Fact]
    public async Task TicketSeries_Endpoint_UnknownAttempt_Is404()
    {
        await _store.AddAsync(Run(R1), Step(R1));

        (await TicketSeriesEndpoints.SeriesAsync("alpha", "42", "no-such-run", _store.Series, CancellationToken.None))
            .Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task TicketSeries_ReviewObjectionCut_GateReadFromTheGateStep_OtherHandbackSkipped()
    {
        var objected = Cut(R1, 3, Entry(A, 1));
        objected.HandbackCase = "ReviewObjection";
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), objected, Gate(R1, "success"),
            Run(R2, status: "waiting_for_input"), Step(R2), Handback(R2, 4, "Question"), Gate(R2, "success"));

        (await ReadAsync(R1)).Gate.Should().Be(new TicketSeriesGateView("passed", 1, R1), "the prefix before the objection was gated");
        (await ReadAsync(R2)).Gate.Should().Be(new TicketSeriesGateView("skipped", 0, R2));
    }

    // 2026-10-13-8762v: the default read carries the runway for a real series only.
    [Fact]
    public async Task TicketSeriesRead_DefaultRead_CarriesTheBudget()
    {
        await _store.AddAsync(Run(R1, cost: 4m), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)), SpecRow(R1, A, "done"),
            Run(R2, cost: 6m), Step(R2), Cut(R2, 5, Entry(A, 1), Entry(B, 2)), SpecRow(R2, B, "not_started"),
            new RunAttempt { RunId = R2, CauseChannel = AgentSmith.Contracts.Runs.AttemptChannels.Series, CarriedSpecIdsJson = $"[\"{A}\"]" });

        var budget = (await ReadAsync()).Budget;

        budget.Should().NotBeNull("the chain holds two attempts");
        budget!.SpentUsd.Should().Be(10m);
        (budget.CapUsd, budget.CapHours, budget.ChainFrom).Should().Be((150m, 12d, 1));
        budget.ElapsedHours.Should().Be(1, "two attempts of thirty minutes each");
        (budget.Next, budget.HeldBecause).Should().Be(("hold", "no-progress"), "the second attempt finished no spec");
        (await ReadAsync(R1)).Budget.Should().BeNull("an attempt's own read carries no runway");
    }

    [Fact]
    public async Task TicketSeriesRead_SingleChunkTicket_HasNoBudget()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)), SpecRow(R1, A, "done"), SpecRow(R1, B, "done"));

        (await ReadAsync()).Budget.Should().BeNull("one attempt within one chunk is no series to run on");
    }

    private static RunStep Gate(string runId, string status) => new()
    {
        RunId = runId, StepIndex = 5, StepName = "Validate the spec", CommandName = CommandNames.SpecGate, Status = status,
    };

    private async Task<TicketSeriesView> ReadAsync(string? runId = null) =>
        (await TicketSeriesEndpoints.SeriesAsync("alpha", "42", runId, _store.Series, CancellationToken.None))
        .Should().BeOfType<Ok<TicketSeriesView>>().Subject.Value!;
}
