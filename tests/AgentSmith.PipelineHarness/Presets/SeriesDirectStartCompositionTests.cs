using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-27-3532a: one master per ticket, through the REAL server composition — the spawn funnel, the series entry, the
/// series store and wakes, the capacity queue, the filing and the master's acts. The boundaries are the tracker, the claim
/// (with the lease it writes) and the sandbox host's capacity. A trigger of a code ticket wakes its master and claims nothing;
/// start_ticket starts it straight through the run-start path; a queue head is still claimed by the trigger's funnel.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesDirectStartCompositionTests
{
    private const string Project = SeriesDirectStartRig.Project;

    // Before, start_ticket tagged and moved the ticket in a 'start' run and claimed it through the label route: a ticket no
    // label routes (this one lacks the project's tag) was answered "nothing routes it now" and never ran.
    [Fact]
    public async Task StartTicket_UnroutedTicket_StartsItsRunWithNoTag()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.SeriesAsync("1");

        var start = await rig.StartTicketAsync("1");

        start.Kind.Should().Be(SeriesStartKind.Started, start.Text);
        rig.Claims.Should().ContainSingle().Which.Should().Match<ClaimRequest>(c => c.PipelineName == PipelinePresets.CodeName
            && c.InitialContext!.ContainsKey(ContextKeys.SeriesAutoStart) && c.ExistingRunId == start.RunId);
        rig.Tracker.Labels.Should().BeEmpty("no tag is written");
        rig.Tracker.Moves.Should().BeEmpty("no 'start' run moves it");
        rig.Events.Started.Should().NotContain(e => e.Pipeline == PipelinePresets.StartName);
    }

    [Fact]
    public async Task StartTicket_NoCapacity_AnswersQueuedWithPlace()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync(room: false);
        await rig.SeriesAsync("1");

        var start = await rig.StartTicketAsync("1");

        start.Kind.Should().Be(SeriesStartKind.Queued, start.Text);
        start.Text.Should().Contain($"is queued as run {start.RunId} (place 1 in the capacity queue)").And.Contain("namespace quota full");
        rig.Claims.Should().BeEmpty();
    }

    [Fact]
    public async Task Trigger_CodeTicket_WakesItsMasterAndClaimsNothing()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.TicketAsync(routed: true);

        var spawned = await rig.TriggerAsync("1");

        spawned.ClaimResults.Should().ContainSingle().Which.Rejection.Should().Be(ClaimRejectionReason.LedBySeries);
        rig.Claims.Should().BeEmpty();
        var series = await rig.HolderAsync("1");
        series.Should().NotBeNull("the trigger opened the ticket's series of one");
        series!.Wakes.Should().ContainSingle().Which.Should().Match<SeriesWake>(w => w.Kind == SeriesWakeKinds.Prerequisite
            && w.TicketId == "1" && w.Once == $"start:{series.Id}:1:none" && w.Text.Contains("start_ticket"));
        SeriesBeatDue.Waking(series, series.Wakes[0]).Should().BeTrue("the wake reaches the master");
    }

    // No double start from trigger and master: a re-poll wakes once, the master starts one run, and a trigger after it meets
    // the run's lease and claims nothing new.
    [Fact]
    public async Task TriggerThenStartTicket_OneRun()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.TicketAsync(routed: true);
        await rig.TriggerAsync("1");
        await rig.TriggerAsync("1");

        var start = await rig.StartTicketAsync("1");
        var after = await rig.TriggerAsync("1");

        start.Kind.Should().Be(SeriesStartKind.Started, start.Text);
        rig.Claims.Should().ContainSingle("exactly one run claimed the ticket");
        after.ClaimResults.Should().ContainSingle().Which.Outcome.Should().Be(ClaimOutcome.AlreadyClaimed);
        (await rig.HolderAsync("1"))!.Wakes.Should().ContainSingle("the re-poll woke the master once");
    }

    // No ticket stuck: a filing's tickets reach their master though no tracker event ever arrives.
    [Fact]
    public async Task Filing_NoTriggerEvent_MasterIsWokenToStart()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.Services.GetRequiredService<IFilingRun>().RunAsync(SeriesDirectStartRig.Conversation(), SeriesDirectStartRig.List(), true, "prop-1");

        var series = await rig.HolderAsync("1");

        series!.Wakes.Select(w => (w.TicketId, w.Once)).Should().Equal(("1", $"start:{series.Id}:1:none"), ("2", $"start:{series.Id}:2:none"));
        rig.Claims.Should().BeEmpty("the filing starts no run itself");
        (await rig.StartTicketAsync("1")).Kind.Should().Be(SeriesStartKind.Started);
        rig.Claims.Should().ContainSingle();
    }

    // No run outside the queue, and a queued ticket keeps its place: the trigger's funnel still claims the head.
    [Fact]
    public async Task Trigger_QueueHead_IsStillClaimed()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.TicketAsync(routed: true);
        var reserved = await rig.Services.GetRequiredService<ICapacityQueue>().EnqueueAsync(new CapacityQueueCandidate(
            Project, "1", PipelinePresets.CodeName, "github", "run-q", "waiting for sandbox capacity", [], "{}", null), default);

        var spawned = await rig.TriggerAsync("1");

        spawned.ClaimResults.Should().ContainSingle().Which.Outcome.Should().Be(ClaimOutcome.Claimed);
        rig.Claims.Should().ContainSingle().Which.ExistingRunId.Should().Be(reserved);
        (await rig.Services.GetRequiredService<ICapacityQueue>().CountAsync(default)).Should().Be(0);
        (await rig.HolderAsync("1")).Should().BeNull("a ticket under way is no entry into a series");
    }

    // A trigger never overwrites a start by name that waits for room: it claims nothing, and the pump claims the head with the
    // series' context, so the head is neither dropped for its status nor read as a trigger's.
    [Fact]
    public async Task StartTicketQueued_ThenTrigger_PumpClaimsWithSeriesAutoStart()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync(room: false);
        await rig.SeriesAsync("1");
        var start = await rig.StartTicketAsync("1");
        rig.Tracker.Labels.Add(("1", Project)); // a person tags it, so the poller's route names it

        var spawned = await rig.TriggerAsync("1");
        await DurableDialogueHarness.BuildPump(rig.Harness, FixturePaths.Filing).TickAsync(default);

        start.Kind.Should().Be(SeriesStartKind.Queued, start.Text);
        spawned.ClaimResults.Should().ContainSingle().Which.Rejection.Should().Be(ClaimRejectionReason.LedBySeries);
        rig.Claims.Should().ContainSingle().Which.Should().Match<ClaimRequest>(c => c.ExistingRunId == start.RunId
            && c.InitialContext!.IsSeriesAutoStart() && c.InitialContext!.IsRequestedByName());
    }

    // A series of one a trigger opened in a filing's window hands its ticket over and ends as done: once the filing's series
    // ended too, a later trigger opens a new series of one instead of reading the handed-over one as a stop.
    [Fact]
    public async Task Filing_AbsorbedSeriesOfOne_DoesNotBlockALaterTrigger()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        await rig.TicketAsync(routed: true);
        await rig.TriggerAsync("1");
        var absorbed = (await rig.HolderAsync("1"))!;
        await rig.SeriesAsync("2");
        await rig.JoinAsync("sr-1", "1");
        await rig.Services.GetRequiredService<ISeriesRunStore>().UpdateAsync("sr-1", r => r with { State = SeriesRunStates.Done }, default);

        var later = await rig.TriggerAsync("1");

        (await rig.Services.GetRequiredService<ISeriesRunStore>().GetAsync(absorbed.Id, default))!.State.Should().Be(SeriesRunStates.Done);
        later.ClaimResults.Should().ContainSingle().Which.Error.Should().Contain("its master was woken");
        (await rig.HolderAsync("1"))!.Id.Should().NotBe(absorbed.Id).And.NotBe("sr-1");
    }

    [Fact]
    public async Task AddTicket_Spec_FilesApprovedSetTheRunCarries()
    {
        await using var rig = await SeriesDirectStartRig.BuildAsync();
        var series = await rig.SeriesAsync("1");
        const string spec = "spec: 2026-11-27-c0de\ngoal: \"The index exists before the API reads it\"\n";

        var added = await rig.Services.GetRequiredService<ISeriesEditActs>().AddTicketAsync(series, spec, [], ["1"], default);
        var start = await rig.StartTicketAsync(added.TicketId!);

        added.Changed.Should().BeTrue(added.Answer);
        start.Kind.Should().Be(SeriesStartKind.Started, start.Text);
        rig.Claims.Should().ContainSingle().Which.InitialContext![ContextKeys.ApprovedSpecSet].Should().BeOfType<string>()
            .Which.Should().Contain("The index exists before the API reads it", "the run carries the spec add_ticket filed as approved (re-id'd into its series)");
    }
}
