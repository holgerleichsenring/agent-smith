using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79a: the one resolver both ScopeRepos and DeriveSpec ask. Two routes — the record
/// CARRIED on the run's initial context, which every launcher can use, and the STORE, which
/// repairs a request nobody built a context for. Where both answer, the newer approval wins; a
/// carried record for another ticket is ignored.
/// </summary>
public sealed class ApprovedSpecSetResolverTests
{
    private const string Key = "azdo-19106";

    [Fact]
    public async Task ApprovedSet_CarriedOnTheContext_IsTheSource()
    {
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon);
        var pipeline = Carrying(SpecApprovalJson.Write(record));

        var resolved = await Resolver(ApprovedSetDoubles.Store()).ResolveAsync(pipeline, new SpecSetKey(Key), default);

        resolved.Should().NotBeNull();
        resolved!.Key.Should().Be(Key);
        resolved.Approval!.At.Should().Be(ApprovedSets.Noon);
        resolved.Set.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p0001a");
    }

    [Fact]
    public async Task ApprovedSet_NotCarriedButInTheStore_IsTheSource()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);

        var resolved = await Resolver(store).ResolveAsync(
            Run("sample", ApprovedSets.Tracker), new SpecSetKey(Key), default);

        resolved.Should().NotBeNull("a process that has a store repairs a request nobody built a context for");
        resolved!.Approval!.At.Should().Be(ApprovedSets.Noon);
    }

    [Fact]
    public async Task ApprovedSet_CarriedRecordOlderThanTheStoredOne_TheStoredOneWins()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(2), conversation: "session-2"), default);
        var pipeline = Carrying(SpecApprovalJson.Write(ApprovedSets.Record(Key, ApprovedSets.Noon)));

        var resolved = await Resolver(store).ResolveAsync(pipeline, new SpecSetKey(Key), default);

        resolved!.Approval!.Conversation.Should().Be("session-2",
            "a queue entry freezes its context when the candidate is built, so the carried copy can be stale");
        SpecApprovalJson.Read(pipeline.Get<string>(ContextKeys.ApprovedSpecSet))!.Approval!.At
            .Should().Be(ApprovedSets.Noon.AddHours(2), "the winner is published back for the next step");
    }

    /// <summary>The run then has no set; that a FILED ticket fails loudly on it is
    /// ApprovedSetDeriveSpecTests.ApprovedSet_CarriedRecordForAnotherTicket_FailsLoudly.</summary>
    [Fact]
    public async Task ApprovedSet_CarriedRecordForAnotherTicket_IsIgnored()
    {
        var pipeline = Carrying(SpecApprovalJson.Write(ApprovedSets.Record("azdo-999", ApprovedSets.Noon)));

        var resolved = await Resolver(ApprovedSetDoubles.Store())
            .ResolveAsync(pipeline, new SpecSetKey(Key), default);

        resolved.Should().BeNull(
            "a re-used queue row must not make this run work to another ticket's specification");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: the spec key is &lt;type&gt;-&lt;ticketId&gt; and carries no
    /// instance, so a record from a SECOND tracker of the same type must not answer for this run.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_CarriedRecordFromAnotherTrackerInstance_IsIgnored()
    {
        var elsewhere = ApprovedSets.Record(Key, ApprovedSets.Noon, tracker: "a-second-azdo");
        var pipeline = Carrying(SpecApprovalJson.Write(elsewhere));

        var resolved = await Resolver(ApprovedSetDoubles.Store())
            .ResolveAsync(pipeline, new SpecSetKey(Key), default);

        resolved.Should().BeNull(
            "two tracker instances numbering a ticket alike are different work");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: the capacity-queue pump and the Redis job payload re-materialize
    /// a request context value as a JsonElement, which is the shape the carry really arrives in.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_CarriedAsAJsonElement_IsReadTheSameAsAString()
    {
        var json = SpecApprovalJson.Write(ApprovedSets.Record(Key, ApprovedSets.Noon));
        var pipeline = Run("sample", ApprovedSets.Tracker);
        // Exactly what the queue round-trip yields: the string value, re-materialized as an element.
        pipeline.Set(ContextKeys.ApprovedSpecSet, JsonSerializer.SerializeToElement(json));

        var resolved = await Resolver(ApprovedSetDoubles.Store())
            .ResolveAsync(pipeline, new SpecSetKey(Key), default);

        resolved.Should().NotBeNull("the queue round-trip is the normal shape, not an edge case");
        resolved!.Set.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p0001a");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: valid JSON that names a key and nothing else deserializes to a
    /// record whose Set is null, and every reader then dereferences it.
    /// </summary>
    [Fact]
    public void SpecApprovalJson_AHollowRecord_ReadsAsNothing()
    {
        SpecApprovalJson.Read("""{"key":"azdo-19106"}""").Should().BeNull();
        SpecApprovalJson.Read("""{"key":"azdo-19106","set":null,"repositories":[]}""").Should().BeNull();
        SpecApprovalJson.Read("""{"set":{"key":"x","phases":[],"revisions":[]}}""")
            .Should().BeNull("a record with no key cannot be checked against the run's own");
    }

    /// <summary>
    /// The record's key is built the way the RUN builds its own: the tracker platform plus the
    /// ticket id. If the two ever drifted apart, every filed ticket would fail loudly.
    /// </summary>
    [Fact]
    public void ApprovedRecord_KeyedByTrackerTypeAndTicketId_MatchesTheRunsSpecSetKey()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TrackerPlatform, "AzureDevOps".ToLowerInvariant());
        var ticket = new Ticket(new TicketId("19106"), "t", "d", null, "open", "azdo", []);

        SpecSetKeyFactory.For(ticket, pipeline).Value
            .Should().Be(SpecSetKey.For("azuredevops", "19106").Value);
    }

    /// <summary>
    /// Keyed by the tracker CONNECTION and the spec key — never by the project. One ticket
    /// matching two projects of ONE tracker is spawned twice and is the same work, so one record
    /// serves both; the same ticket number on a SECOND instance of that tracker is not.
    /// </summary>
    [Fact]
    public async Task ApprovedRecord_TwoProjectsOnOneTracker_ShareOneRecordButTwoTrackersDoNot()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);
        await store.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, ["p0009z"], tracker: "a-second-azdo"), default);
        var resolver = Resolver(store);

        var first = await resolver.ResolveAsync(Run("project-a", ApprovedSets.Tracker), new SpecSetKey(Key), default);
        var second = await resolver.ResolveAsync(Run("project-b", ApprovedSets.Tracker), new SpecSetKey(Key), default);
        var elsewhere = await resolver.ResolveAsync(Run("project-c", "a-second-azdo"), new SpecSetKey(Key), default);

        first!.Set.Phases[0].PhaseId.Should().Be("p0001a");
        second!.Set.Phases[0].PhaseId.Should().Be("p0001a",
            "the record is keyed by the ticket, never by the project");
        elsewhere!.Set.Phases[0].PhaseId.Should().Be("p0009z",
            "a second instance of the same tracker type holds its own ticket 19106");
    }

    [Fact]
    public void SpecApproval_ANewerInstant_IsNewerThanAnOlderOneAndThanNone()
    {
        var newer = ApprovedSets.Approval(ApprovedSets.Noon.AddMinutes(1));

        newer.IsNewerThan(ApprovedSets.Approval(ApprovedSets.Noon)).Should().BeTrue();
        newer.IsNewerThan(null).Should().BeTrue("a set nobody approved is beaten by any approval");
        ApprovedSets.Approval(ApprovedSets.Noon).IsNewerThan(newer).Should().BeFalse();
    }

    [Fact]
    public void SpecApprovalJson_ARecord_SurvivesTheRoundTripWhole()
    {
        var record = ApprovedSets.Record(
            Key, ApprovedSets.Noon, ["p0001a", "p0001b"], ["sample-api", "sample-web"]);

        var read = SpecApprovalJson.Read(SpecApprovalJson.Write(record));

        read!.Key.Should().Be(Key);
        read.Repositories.Should().Equal("sample-api", "sample-web");
        read.Set.Phases.Select(p => p.PhaseId).Should().Equal("p0001a", "p0001b");
        read.Approval.Should().Be(record.Approval);
    }

    [Fact]
    public void SpecApprovalJson_TextThatIsNotARecord_ReadsAsNothing() =>
        SpecApprovalJson.Read("{ not json").Should().BeNull();

    private static ApprovedSpecSetResolver Resolver(ISpecApprovalStore store) =>
        new(store, NullLogger<ApprovedSpecSetResolver>.Instance);

    private static PipelineContext Carrying(string json)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TrackerConnection, ApprovedSets.Tracker);
        pipeline.Set(ContextKeys.ApprovedSpecSet, json);
        return pipeline;
    }

    private static PipelineContext Run(string project, string tracker)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, project);
        pipeline.Set(ContextKeys.TrackerConnection, tracker);
        return pipeline;
    }
}
