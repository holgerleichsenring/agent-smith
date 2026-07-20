using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using EventEnvelopeSerializer = AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0344b: the run story is REAL data end to end — the p0341 progress ledger and
/// the p0340 acceptance dispositions are snapshotted at run end
/// (RunStoryRecordedEvent), persisted as JSON columns on the run row by the
/// applier, and served on the run detail in the exact camelCase wire shape the
/// dashboard reads. Old rows serve honest nulls.
/// </summary>
public sealed class RunStoryServedTests : IDisposable
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-16T10:00:00Z");
    private readonly SqliteConnection _connection;

    public RunStoryServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // p0344b spec test: RunDetail_PersistedLedgerAndDispositions_Served
    [Fact]
    public async Task RunDetail_PersistedLedgerAndDispositions_Served()
    {
        const string runId = "2026-07-16T10-00-00-0001";
        var ledgerJson = RunStorySnapshotBuilder.BuildLedgerJson(new ProgressLedger(
        [
            // p0356: the Note persists too — the stored ledger doubles as the
            // same-ticket resume seed and the note is its working state.
            new ProgressLedgerEntry("1", "Add the endpoint", ProgressStatus.Done, "src/Api/Endpoint.cs",
                Note: "returns 200 with the slim DTO"),
            new ProgressLedgerEntry("2", "Wire the client", ProgressStatus.InProgress),
            new ProgressLedgerEntry("3", "Extend the smoke test", ProgressStatus.Pending),
        ]))!;
        var acceptanceJson = RunStorySnapshotBuilder.BuildAcceptanceJson(
            new RatifiedExpectation(
                new ExpectationDraft("Observed state",
                    ["The endpoint returns 200", "Old rows serve null", "A third criterion"],
                    [], null),
                ExpectationOutcomes.Verbatim, "operator", T, 0),
            new MasterVerification(VerificationStatus.Green, true, true, true, true, "green",
                AcceptanceDispositions:
                [
                    new AcceptanceDisposition("The endpoint returns 200", AcceptanceStatus.Met, "Endpoint.cs"),
                    new AcceptanceDisposition("Old rows serve null", AcceptanceStatus.NotApplicable, "no old rows in scope"),
                    // no disposition for the third criterion → unproven
                ]))!;

        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new StepStartedEvent(runId, 0, "Fetch ticket", 9, T, "Fetch ticket", CommandNames.FetchTicket),
            new StepFinishedEvent(runId, 0, "success", 100, T),
            new RunStoryRecordedEvent(runId, ledgerJson, acceptanceJson, T),
            new RunFinishedEvent(runId, "success", null, "done", T.AddMinutes(5)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(runId, CancellationToken.None);
        var snap = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        snap.ProgressLedger.Should().NotBeNull().And.HaveCount(3);
        snap.ProgressLedger![0].Should().Be(
            new ProgressLedgerItemView("1", "Add the endpoint", "done", "src/Api/Endpoint.cs",
                "returns 200 with the slim DTO"));
        snap.ProgressLedger[1].Status.Should().Be("in_progress");
        snap.ProgressLedger[2].Status.Should().Be("pending");

        snap.Acceptance.Should().NotBeNull();
        snap.Acceptance!.Outcome.Should().Be("verbatim");
        snap.Acceptance.RatifiedBy.Should().Be("operator");
        snap.Acceptance.Criteria.Should().HaveCount(3);
        snap.Acceptance.Criteria[0].Should().Be(
            new AcceptanceCriterionView("The endpoint returns 200", "met", "Endpoint.cs"));
        snap.Acceptance.Criteria[1].Should().Be(
            new AcceptanceCriterionView("Old rows serve null", "not_applicable", "no old rows in scope"));
        snap.Acceptance.Criteria[2].Should().Be(
            new AcceptanceCriterionView("A third criterion", "unproven", null),
            "a criterion the master reported nothing for is visibly unproven, never dropped");

        // The exact camelCase wire contract the dashboard reads.
        var wire = JsonSerializer.Serialize(snap, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wire.Should().Contain("\"progressLedger\":[{\"id\":\"1\",\"activity\":\"Add the endpoint\",\"status\":\"done\",\"target\":\"src/Api/Endpoint.cs\",\"note\":\"returns 200 with the slim DTO\"}");
        wire.Should().Contain("\"acceptance\":{\"criteria\":[{\"text\":\"The endpoint returns 200\",\"status\":\"met\",\"reason\":\"Endpoint.cs\"}");
        wire.Should().Contain("\"outcome\":\"verbatim\"").And.Contain("\"ratifiedBy\":\"operator\"");
        wire.Should().Contain("\"beats\":{\"ticket\":");
    }

    // p0344b spec test: RunDetail_PreMigrationRow_BeatsNull_ClientRendersNoStorybar
    // (server half — a pre-p0344b row serves beats/progressLedger/acceptance = null).
    [Fact]
    public async Task RunDetail_PreMigrationRow_BeatsAndStoryNull()
    {
        const string runId = "2026-07-16T10-00-00-0002";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["primary"], T),
            // pre-p0344b producer: no CommandName on the step event
            new StepStartedEvent(runId, 0, "Fetch ticket", 9, T),
            new RunFinishedEvent(runId, "success", null, "done", T.AddMinutes(5)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(runId, CancellationToken.None);
        var snap = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        snap.Beats.Should().BeNull("unmappable stored data must not be guessed into a storybar");
        snap.ProgressLedger.Should().BeNull();
        snap.Acceptance.Should().BeNull();
    }

    [Fact]
    public async Task RunList_ServesBeats_ButNotTheStoryPayloads()
    {
        const string runId = "2026-07-16T10-00-00-0003";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["primary"], T),
            new StepStartedEvent(runId, 0, "Fetch ticket", 9, T, "Fetch ticket", CommandNames.FetchTicket),
            new RunStoryRecordedEvent(runId, "[]", null, T),
            new RunFinishedEvent(runId, "failed", null, "broke", T.AddMinutes(5)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(runId, CancellationToken.None);
        var listSnap = RunSnapshotMapper.ToSnapshot(run!); // list path: includeStory false

        listSnap.Beats.Should().NotBeNull("beats ride the list too");
        listSnap.ProgressLedger.Should().BeNull("the list stays lean");
        listSnap.Acceptance.Should().BeNull();
    }

    [Fact]
    public void StoryBuilder_NoLedgerAndNoContract_BuildsNulls()
    {
        RunStorySnapshotBuilder.BuildLedgerJson(null).Should().BeNull();
        RunStorySnapshotBuilder.BuildLedgerJson(ProgressLedger.Empty).Should().BeNull();
        RunStorySnapshotBuilder.BuildAcceptanceJson(null, null).Should().BeNull();
    }

    [Fact]
    public void StoryBuilder_ContractWithoutAnyDispositions_AllCriteriaUnproven()
    {
        var json = RunStorySnapshotBuilder.BuildAcceptanceJson(
            new RatifiedExpectation(
                new ExpectationDraft("obs", ["c1", "c2"], [], null),
                ExpectationOutcomes.Unratified, "auto", T, 0),
            verification: null)!;

        var view = RunStoryJson.TryDeserialize<AcceptanceView>(json)!;
        view.Criteria.Should().OnlyContain(c => c.Status == "unproven");
        view.Outcome.Should().Be("unratified");
    }

    [Fact]
    public void RunStoryRecordedEvent_RoundTripsThroughTheEnvelopeSerializer()
    {
        var ev = new RunStoryRecordedEvent("r1", "[{\"id\":\"1\"}]", null, T);
        var envelope = EventEnvelopeSerializer.Serialize(ev);
        var back = EventEnvelopeSerializer.Deserialize(envelope);

        back.Should().BeOfType<RunStoryRecordedEvent>()
            .Which.ProgressLedgerJson.Should().Be("[{\"id\":\"1\"}]");
    }

    private async Task ApplyAsync(params RunEvent[] events)
    {
        var applier = new RunEventApplier();
        foreach (var ev in events)
        {
            await using var uow = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(uow, ev, CancellationToken.None);
        }
    }
}
