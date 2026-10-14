using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-8762m: an executed spec's document carries the build note out of the executing attempt's
/// record artifact — the note's only home — or says the master left none.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketSeriesBuildNoteTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketSeriesSpecComposer_ExecutedSpec_CarriesTheRecordsBuildNote()
    {
        var note = new SpecBuildNote(A, ["Bus -> MessageBus"], [], ["table RunDefaults"], []);
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)),
            SpecRow(R1, A, "done"), SpecRow(R1, B, "done"),
            Record(R1, A, new SpecBuildNoteRead(note, false)), Record(R1, B, SpecBuildNoteRead.NoNote));

        var specs = (await ReadAsync()).Specs;

        specs[0].BuildNote.Should().BeEquivalentTo(
            new TicketSeriesBuildNoteView(false, ["Bus -> MessageBus"], [], ["table RunDefaults"], []));
        specs[1].BuildNote!.Missing.Should().BeTrue();
    }

    [Fact]
    public async Task TicketSeriesSpecComposer_NoRecord_HasNoBuildNote()
    {
        await _store.AddAsync(Run(R1), Step(R1), Cut(R1, 3, Entry(A, 1)), SpecRow(R1, A, "in_progress"));

        (await ReadAsync()).Specs.Single().BuildNote.Should().BeNull();
    }

    private static RunArtifact Record(string runId, string specId, SpecBuildNoteRead handForward) => new()
    {
        RunId = runId, Kind = RunSpecProjection.RecordKindPrefix + specId,
        Content = $"spec: {specId}\n" + SpecOutcomeBlock.Render(runId, "delivered", string.Empty, handForward),
    };

    private async Task<TicketSeriesView> ReadAsync() =>
        (await TicketSeriesEndpoints.SeriesAsync("alpha", "42", null, _store.Series, CancellationToken.None))
        .Should().BeOfType<Ok<TicketSeriesView>>().Subject.Value!;
}
