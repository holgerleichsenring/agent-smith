using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.SpecDialog.Continuation.SeriesEditTests;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-11-27-3532d: a worked ticket's spec retired for a replacement between attempts — an appended spec naming it in
/// supersedes, recorded through the one series edit. The retired spec stays, the replacement stands behind it under a
/// minted id, what required the retired spec requires the replacement, and the next attempt reads it so.
/// </summary>
public sealed class SeriesRetireTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private const string Poster = "ops-2";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c", D = Series + "d";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static string Replacement(string id = "2026-10-14-0000") => $"spec: {id}\nsupersedes: [{B}]\ngoal: \"B, done another way\"\n";

    private static string Requiring(string id, string required) => $"spec: {id}\ngoal: \"{id} after the replacement\"\nrequires: [{required}]\n";

    [Fact]
    public async Task Edit_RetireAnUnstartedSpec_RecordsTheReplacementAfterItAndRelinks()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(
            new SeriesEdit(SeriesEditKinds.Recut, C, Requiring(C, B)),
            new SeriesEdit(SeriesEditKinds.Append, Yaml: Replacement()));

        answer.Should().Contain($"{B}: kept (retired").And.Contain($"{D}: appended");
        var record = (await _h.RecordAsync())!;
        record.Set.Specs.Select(s => s.SpecId).Should().Equal(B, D, C);
        record.Set.Specs[1].Draft.Supersedes.Should().Equal(B);
        record.Set.Specs[2].Draft.Requires.Should().Equal(D);
        record.Set.Specs[2].Draft.Yaml.Should().Contain($"requires: [{D}]");
    }

    [Fact]
    public async Task Edit_RetireASpecThatRan_IsRefused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Append, Yaml: $"spec: 2026-10-14-0000\nsupersedes: [{A}]\ngoal: \"g\"\n"));

        answer.Should().StartWith("Nothing was recorded").And.Contain($"'{A}' already ran, so it is never retired");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Edit_DroppingAReplacementWhileItsRetiredSpecStays_IsRefused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Append, Yaml: Replacement()),
            new SeriesEdit(SeriesEditKinds.Drop, "2026-10-14-0000"));

        answer.Should().StartWith("Nothing was recorded").And.Contain($"would reopen {B}");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task NextAttempt_ARetiredSpec_IsKeptAndItsReplacementRunsInItsPlace()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);
        await EditAsync(new SeriesEdit(SeriesEditKinds.Recut, C, Requiring(C, B)), new SeriesEdit(SeriesEditKinds.Append, Yaml: Replacement()));

        var reading = await (await PortsAsync())!.Read.ReadAsync(default);

        reading.Specs.Select(s => (s.Draft.SpecId, s.State)).Should().Equal(
            (A, "not_started"), (B, "superseded"), (D, "not_started"), (C, "not_started"));
        reading.Specs[3].Draft.Requires.Should().Equal(D);
    }

    [Fact]
    public async Task RetireSpec_TheSeriesMaster_RecordsThroughTheTicketsSeriesEdit()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);
        var series = new SeriesRunRecord("series-1", ContinuationHarness.SessionId, "alpha", Poster, true, "active",
            [], TicketStoreHarnessTime);

        var answer = await _h.InScopeAsync(sp => new SeriesSpecRetirement(sp.GetRequiredService<TicketProjects>(),
                sp.GetRequiredService<SeriesEditAct>(), new SpecDraftValidator(new Application.Services.Validation.SpecSchemaProvider()),
                new SpecDraftReader(), new SpecDraftIdRewriter())
            .RetireAsync(series, ContinuationHarness.TicketId, B, $"spec: {B}\ngoal: \"B, done another way\"\n", default));

        answer.Changed.Should().BeTrue(answer.Answer);
        answer.Answer.Should().Contain($"{D}: appended");
        var record = (await _h.RecordAsync())!;
        record.Set.Specs.Select(s => s.SpecId).Should().Equal(B, D, C);
        record.Set.Specs[1].Draft.Yaml.Should().Contain($"spec: {D}").And.Contain($"supersedes: [{B}]");
        record.Approval!.Principal.Should().Be(Poster, "the series' approver records the edit");
    }

    private static readonly DateTimeOffset TicketStoreHarnessTime = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private Task<BoundSeries?> PortsAsync() => _h.InScopeAsync(sp =>
        sp.GetRequiredService<BoundSeriesPorts>().ForAsync(_h.Config.Projects["alpha"], ContinuationHarness.TicketId,
            ContinuationHarness.SessionId, new TurnPoster(Poster, true), default));

    private async Task<string> EditAsync(params SeriesEdit[] edits) =>
        await (await PortsAsync())!.Edit!.EditAsync(edits, default);
}
