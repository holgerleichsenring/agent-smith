using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-14-da5ee: in a worked ticket's conversation the agent reads the series as the next attempt will read it
/// and changes it — recorded as an approval without a start, by the poster, and refused whole where an approval
/// would be.
/// </summary>
public sealed class SeriesEditTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private const string Poster = "ops-2";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c", D = Series + "d";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task ReadSeries_WorkedTicket_ListsSpecsWithTheirStateRanAndYaml()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.AddAsync(TicketSeriesFixtures.SpecRow("r-1", A, "done"), TicketSeriesFixtures.SpecRow("r-1", B, "failed"));

        var reading = await (await PortsAsync())!.Read.ReadAsync(default);

        reading.SeriesId.Should().Be(Series);
        reading.Specs.Select(s => (s.Draft.SpecId, s.State, s.Ran)).Should().Equal(
            (A, "done", true), (B, "failed", false), (C, "not_started", false));
        reading.Specs[1].Draft.Yaml.Should().Be($"spec: {B}\n");
        reading.ShaByRepo.Should().ContainKey("web").WhoseValue.Should().Be("sha");
        ReadSeriesToolHost.Render(reading).Should().Contain($"## {B} — failed, not run").And.Contain($"spec: {B}");
    }

    [Fact]
    public async Task EditSeries_RecutDropAppend_RecordsWithoutStarting_AndNudgesActivity()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditThroughToolAsync(
            new SeriesEdit(SeriesEditKinds.Recut, B, Spec(B, "B, re-cut")),
            new SeriesEdit(SeriesEditKinds.Drop, C),
            new SeriesEdit(SeriesEditKinds.Append, Yaml: Spec("2026-10-14-0000", "New work")));

        answer.Should().Contain("nothing was started").And.Contain($"{D}: appended");
        var record = (await _h.RecordAsync())!;
        record.Continuation!.StartsAttempt.Should().BeFalse("an edit is an approval without a start");
        record.Continuation.BasisRunId.Should().Be("r-1");
        record.Continuation.Dispositions.Select(d => (d.SpecId, d.Kind)).Should().Equal(
            (A, SpecDispositionKinds.Kept), (B, SpecDispositionKinds.Recut), (D, SpecDispositionKinds.Appended),
            (C, SpecDispositionKinds.Dropped));
        record.Set.Specs.Select(s => (s.SpecId, s.Draft.Goal)).Should().Equal((B, "B, re-cut"), (D, "New work"));
        record.Approval!.Principal.Should().Be(Poster, "the poster approved, not the conversation's owner");
        record.Approval.Conversation.Should().Be(ContinuationHarness.SessionId);
        (await _h.NudgesAsync()).Should().BeEmpty("nothing is started");
        _h.Activity.Verify(a => a.ChangedAsync("alpha", ContinuationHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditSeries_SecondEdit_KeepsTheFirst()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);
        await EditAsync(new SeriesEdit(SeriesEditKinds.Recut, B, Spec(B, "B, re-cut")));

        await EditAsync(new SeriesEdit(SeriesEditKinds.Append, Yaml: Spec("2026-10-14-0000", "New work")));

        var record = (await _h.RecordAsync())!;
        record.Set.Specs.Select(s => (s.SpecId, s.Draft.Goal)).Should().Equal((B, "B, re-cut"), (C, "New work"));
        var reading = await (await PortsAsync())!.Read.ReadAsync(default);
        reading.Specs.Select(s => (s.Draft.SpecId, s.Draft.Goal)).Should().Equal(
            (A, "Goal of " + A), (B, "B, re-cut"), (C, "New work"));
    }

    [Fact]
    public async Task EditSeries_ExecutedSpec_IsDiscardedWithTheReason()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Recut, A, Spec(A, "A rewritten")));

        answer.Should().Contain($"{A}: kept (it already ran, so the changed text is discarded)");
        var record = (await _h.RecordAsync())!;
        record.Continuation!.Dispositions.Should().Contain(new SpecDisposition(A, SpecDispositionKinds.Kept));
        record.Set.Specs.Should().NotContain(s => s.SpecId == A, "what ran is never recorded again");
    }

    [Fact]
    public async Task EditSeries_RecutWithAnotherId_Refused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Recut, B, Spec(C, "Another id")));

        answer.Should().StartWith("Nothing was recorded").And.Contain("a recut keeps the spec's id");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task EditSeries_InvalidYaml_Refused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Append, Yaml: "spec: not-an-id\n"));

        answer.Should().Contain("not a valid spec");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task EditSeries_EmptyBasis_Refused()
    {
        await _h.AddAsync(TicketStoreHarness.Run("r-1", status: RunStatuses.Failed), TicketStoreHarness.Step("r-1"));
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Append, Yaml: Spec("2026-10-14-0000", "New work")));

        answer.Should().Contain("published a series").And.Contain("Nothing was recorded");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task EditSeries_ClosedTicket_Refused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);
        (_h.Tracker.Status, _h.Tracker.IsClosed) = ("Closed", true);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Drop, B));

        answer.Should().Contain("closed by a person");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task EditSeries_PendingQuestion_Refused()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.WaitingForInput);
        await _h.AddAsync(TicketStoreHarness.Checkpoint("r-1"));
        await _h.BindAsync(null);

        var answer = await EditAsync(new SeriesEdit(SeriesEditKinds.Drop, B));

        answer.Should().Contain("waiting for an answer");
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task EditSeries_OverTheCap_RefusedWhole()
    {
        var members = Enumerable.Range(0, SpecSet.MaxSpecs).Select(i => Series + (char)('a' + i)).ToList();
        await _h.WorkedAsync("r-1", "alpha", Series, members, executed: [members[0]], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var answer = await EditAsync(
            new SeriesEdit(SeriesEditKinds.Recut, members[1], Spec(members[1], "re-cut")),
            new SeriesEdit(SeriesEditKinds.Append, Yaml: Spec("2026-10-14-0000", "One too many")));

        answer.Should().Contain("Nothing was recorded").And.Contain($"{SpecSet.MaxSpecs}");
        (await _h.RecordAsync()).Should().BeNull("an over-cap edit records none of its changes");
    }

    [Fact]
    public async Task SeriesTools_NeverWorkedTicket_NotOffered()
    {
        await _h.BindAsync(null);

        (await PortsAsync()).Should().BeNull("a never-worked ticket's series is formed in Work it out");
    }

    [Fact]
    public async Task SeriesTools_PosterWithoutRunsControl_ReadsButCannotEdit()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);

        var ports = await PortsAsync(mayStartRuns: false);

        ports!.Read.Should().NotBeNull();
        ports.Edit.Should().BeNull("an edit outside the card needs runs.control");
    }

    private Task<BoundSeries?> PortsAsync(bool mayStartRuns = true) => _h.InScopeAsync(sp =>
        sp.GetRequiredService<BoundSeriesPorts>().ForAsync(_h.Config.Projects["alpha"], ContinuationHarness.TicketId,
            ContinuationHarness.SessionId, new TurnPoster(Poster, mayStartRuns), default));

    private async Task<string> EditAsync(params SeriesEdit[] edits) =>
        await (await PortsAsync())!.Edit!.EditAsync(edits, default);

    // The tool the master calls, with its edits as the model sends them.
    private async Task<string> EditThroughToolAsync(params SeriesEdit[] edits)
    {
        var tool = new EditSeriesToolHost((await PortsAsync())!.Edit!).GetTools(null, null).Single();
        var json = JsonSerializer.SerializeToElement(edits.Select(e => new { kind = e.Kind, specId = e.SpecId, yaml = e.Yaml }));
        var answer = await tool.InvokeAsync(new AIFunctionArguments { ["edits"] = json });
        return answer is JsonElement { ValueKind: JsonValueKind.String } text ? text.GetString()! : answer!.ToString()!;
    }

    internal static string Spec(string id, string goal) => $"spec: {id}\ngoal: \"{goal}\"\n";
}
