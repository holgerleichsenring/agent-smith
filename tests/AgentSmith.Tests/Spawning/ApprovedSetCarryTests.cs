using System.Text.Json;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// 2026-09-17-0e79a: the spawn funnel puts the WHOLE approved record on the initial context of
/// both shapes it emits — the claim for an admitted head ticket and the capacity-queue candidate
/// for a deferred one — so a run launched now and a run the pump launches later work from the same
/// approved set.
/// </summary>
public sealed class ApprovedSetCarryTests
{
    private const string Key = "github-42";

    [Fact]
    public async Task SpawnFunnel_TicketWithARecord_PutsTheWholeRecordOnBothRequestShapes()
    {
        var json = await CarriedJsonAsync(seeded: true);

        var request = SpawnRequestBuilder.BuildRequest(
            Project(), "code", Envelope(), new WebhookTriggerConfig(), null, "run-1", json);
        var candidate = SpawnRequestBuilder.BuildCandidate(
            Project(), "code", Envelope(), new WebhookTriggerConfig(), null, "run-2", "queued", json);

        var carried = SpecApprovalJson.Read(
            (string)request.InitialContext![ContextKeys.ApprovedSpecSet]);
        carried!.Key.Should().Be(Key);
        carried.Approval!.At.Should().Be(ApprovedSets.Noon);
        carried.Set.Phases.Should().ContainSingle("the whole record travels, not a pointer at it");

        JsonSerializer.Deserialize<Dictionary<string, object>>(candidate.InitialContextJson!)
            .Should().ContainKey(ContextKeys.ApprovedSpecSet);
    }

    [Fact]
    public async Task SpawnFunnel_TicketWithoutARecord_AddsNothing()
    {
        var json = await CarriedJsonAsync(seeded: false);

        json.Should().BeNull();
        (SpawnRequestBuilder.BuildRequest(
                Project(), "code", Envelope(), new WebhookTriggerConfig(), null, "run-1", json)
            .InitialContext ?? [])
            .Should().NotContainKey(ContextKeys.ApprovedSpecSet,
                "a ticket nobody approved carries exactly what it did before");
    }

    /// <summary>
    /// A deferred entry freezes its context when the candidate is built and the pump launches it
    /// later — so the record has to be IN the queue row, not looked up at launch.
    /// </summary>
    [Fact]
    public async Task SpawnFunnel_DeferredEntry_CarriesTheRecordThroughTheQueueRow()
    {
        var json = await CarriedJsonAsync(seeded: true);

        var candidate = SpawnRequestBuilder.BuildCandidate(
            Project(), "code", Envelope(), new WebhookTriggerConfig(), null, "run-2", "queued", json);

        var context = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            candidate.InitialContextJson!)!;
        SpecApprovalJson.Read(context[ContextKeys.ApprovedSpecSet].GetString())!.Key.Should().Be(Key);
    }

    [Fact]
    public async Task ApprovedSetCarrier_ARecordedTicket_YieldsAOneKeyInitialContext()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);

        var context = await ApprovedSetDoubles.Carrier(store).ContextForAsync(Tracker(), "42", default);

        context.Should().ContainSingle().Which.Key.Should().Be(ContextKeys.ApprovedSpecSet);
        (await ApprovedSetDoubles.Carrier(store).ContextForAsync(Tracker(), "99", default))
            .Should().BeNull("another ticket's run carries nothing");
    }

    /// <summary>A request with no platform or no ticket has no spec key, so it has no record —
    /// even when the store holds one for a ticket of that number on some other tracker.</summary>
    [Fact]
    public async Task ApprovedSetCarrier_NoPlatformOrNoTicket_AsksTheStoreNothing()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);

        (await ApprovedSetDoubles.Carrier(store).JsonForAsync(ApprovedSets.Tracker, null, "42", default))
            .Should().BeNull();
        (await ApprovedSetDoubles.Carrier(store).JsonForAsync(ApprovedSets.Tracker, "github", null, default))
            .Should().BeNull();
    }

    private static async Task<string?> CarriedJsonAsync(bool seeded)
    {
        var store = ApprovedSetDoubles.Store();
        if (seeded) await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);
        return await ApprovedSetDoubles.Carrier(store)
            .JsonForAsync(ApprovedSets.Tracker, "github", "42", default);
    }

    /// <summary>
    /// 2026-09-17-0e79a review: a record is identified by its tracker CONNECTION as well as its
    /// spec key, so a second instance of the same tracker type does not answer for this one.
    /// </summary>
    [Fact]
    public async Task ApprovedSetCarrier_AnotherTrackerInstanceOfTheSameType_CarriesNothing()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);

        (await ApprovedSetDoubles.Carrier(store)
            .JsonForAsync("a-second-github", "github", "42", default))
            .Should().BeNull("ticket 42 on another instance is different work with the same number");
    }

    private static TrackerConnection Tracker() =>
        new() { Name = ApprovedSets.Tracker, Type = TrackerType.GitHub };

    private static ResolvedProject Project() => new() { Name = "sample", Repos = [] };

    private static IncomingTicketEnvelope Envelope() => new()
    {
        TicketId = "42",
        Platform = "github",
        Labels = [],
    };
}
