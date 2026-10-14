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
/// 2026-10-13-9a59f: the series read names, per spec step, its last recorded state and the attempt and
/// run-wide pass that moved it to done — folded from the attempts' ledger transitions over the real store.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketSeriesStepsWorkedTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string A = "2026-10-13-1234a";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketSeries_Read_StepDoneInSecondPass_NamesAttemptAndPass()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1)),
            Moved(R1, 4, pass: 1, ("s1", "pending", "in_progress")),
            Run(R2), Step(R2), Cut(R2, 5, Entry(A, 1)),
            Moved(R2, 6, pass: 1, ("s1", "in_progress", "pending")),
            Moved(R2, 7, pass: 2, ("s1", "pending", "done")),
            Moved(R2, 8, pass: 3, ("s1", "done", "done"))); // a refused rewrite moved nothing

        var worked = (await ReadAsync()).Specs.Single().Worked;

        worked.Should().Equal(new TicketSeriesStepWorkedView("s1", "done", R2, 2, 2));
    }

    [Fact]
    public async Task TicketSeries_Read_StepNeverMoved_HasNoState()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1)));

        var worked = (await ReadAsync()).Specs.Single().Worked;

        worked.Should().Equal(new TicketSeriesStepWorkedView("s1", null, null, null, null));
    }

    private static RunLedgerTransition Moved(string runId, long seq, int pass, (string Id, string From, string To) move) => new()
    {
        RunId = runId, EventSeq = seq, SpecId = A, MasterPass = pass, At = T.AddMinutes(seq),
        TransitionsJson = $$"""[{"entryId":"{{move.Id}}","activity":"x","from":"{{move.From}}","to":"{{move.To}}","cause":"model_update","pass":0}]""",
    };

    private async Task<TicketSeriesView> ReadAsync() =>
        (await TicketSeriesEndpoints.SeriesAsync("alpha", "42", null, _store.Series, CancellationToken.None))
        .Should().BeOfType<Ok<TicketSeriesView>>().Subject.Value!;
}
