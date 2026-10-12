using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Persistence.SpecRename.OldShapeStore;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: what a rewritten store must hold — every surface in the spec names, read by
/// this release's own readers into exactly the values the old shape read into.
/// </summary>
internal static class SpecNamedStoreAssertions
{
    private static readonly EventEnvelopeSerializer Codec = new();

    public static async Task HoldsOnlySpecNamesAsync(AgentSmithDbContext db) =>
        (await TextsAsync(db)).Should().OnlyContain(t => t != null && !t.Contains("Phase") && !t.Contains("phase"));

    /// <summary>Every rewritten text but the LLM call's — before the rewrite, each says phase but
    /// the one spec_record row an old store can already hold beside its phase_record twin.</summary>
    public static async Task<IReadOnlyList<string?>> TextsAsync(AgentSmithDbContext db)
    {
        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.Id == RunId);
        var checkpoint = await db.RunCheckpoints.AsNoTracking().SingleAsync();
        var session = await db.SpecDialogSessions.AsNoTracking().SingleAsync();
        var events = await db.RunEvents.AsNoTracking().Where(e => e.Seq != LlmCall.Seq).ToListAsync();
        string?[] texts =
        [
            run.PlannedStepsJson, run.QueuedRequestJson, run.AcceptanceJson,
            checkpoint.RemainingCommandsJson, checkpoint.ContextJson,
            (await db.QueuedTickets.AsNoTracking().SingleAsync()).InitialContextJson,
            (await db.ApprovedSeries.AsNoTracking().SingleAsync()).ContentJson,
            session.TranscriptJson, session.LatestProposalJson, session.ConfirmedOutcomeJson, session.LatestFilingJson,
            .. events.Select(e => e.Type + e.PayloadJson),
            .. await db.RunArtifacts.AsNoTracking().Select(a => a.Kind).ToListAsync(),
            .. await db.RunSteps.AsNoTracking().Select(s => s.CommandName).ToListAsync(),
        ];
        return texts;
    }

    public static async Task ReadsBackAsTheOldShapeDidAsync(AgentSmithDbContext db)
    {
        var events = await db.RunEvents.AsNoTracking().OrderBy(e => e.Seq).ToListAsync();
        events.Select(e => e.Type).Should().StartWith(
            [nameof(EventType.SpecStateChanged), nameof(EventType.SpecRecorded), nameof(EventType.SpecReviewed)]);
        Codec.DeserializeRaw(events[0].Type, events[0].PayloadJson).Should().BeOfType<SpecStateChangedEvent>()
            .Which.SpecId.Should().Be("s1");
        Codec.DeserializeRaw(events[3].Type, events[3].PayloadJson).Should().BeOfType<StepStartedEvent>()
            .Which.CommandName.Should().Be(CommandNames.SelectSpec);
        ReadsCheckpoint(await db.RunCheckpoints.AsNoTracking().SingleAsync());
        await ReadsRunAsync(db);
        await ReadsDialogAsync(db);
        (await db.RunArtifacts.AsNoTracking().ToDictionaryAsync(a => a.Kind, a => a.Content)).Should().BeEquivalentTo(
            new Dictionary<string, string?> { ["spec_record:s1"] = "spec: s1", ["spec_review:s1"] = "{}", ["spec_record:s2"] = "newer" });
        (await db.RunSteps.AsNoTracking().SingleAsync()).Should().Match<AgentSmith.Infrastructure.Persistence.Entities.RunStep>(
            s => s.CommandName == CommandNames.VerifySpec + ":api" && s.SpecId == "s1");
        (await db.RunDecisions.AsNoTracking().SingleAsync()).SpecId.Should().Be("s1");
    }

    // The record compared as this release writes it: its computed members are not comparable values.
    private static string? ApprovalOf(string? text) =>
        SpecApprovalJson.Read(text) is { } record ? SpecApprovalJson.Write(record) : null;

    private static void ReadsCheckpoint(AgentSmith.Infrastructure.Persistence.Entities.RunCheckpoint checkpoint)
    {
        var restored = new PipelineContext();
        new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance).Restore(checkpoint.ContextJson, restored);
        restored.Get<SpecDraft>(ContextKeys.CurrentSpec).Should().BeEquivalentTo(OldShapeJson.Draft);
        restored.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Should().BeEquivalentTo(OldShapeJson.Progress);
        ApprovalOf(restored.Get<string>(ContextKeys.ApprovedSpecSet)).Should().Be(SpecApprovalJson.Write(OldShapeJson.Approval));
        JsonSerializer.Deserialize<List<CheckpointCommand>>(checkpoint.RemainingCommandsJson)!.First()
            .Should().Be(new CheckpointCommand(CommandNames.VerifySpec + ":api", SpecId: "s1"));
    }

    private static async Task ReadsRunAsync(AgentSmithDbContext db)
    {
        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.Id == RunId);
        RunStoryJson.TryDeserialize<List<PlannedStepView>>(run.PlannedStepsJson)!.Single()
            .Should().Be(new PlannedStepView(4, CommandNames.SelectSpec, "Start the spec", "s1"));
        RunStoryJson.TryDeserialize<AcceptanceView>(run.AcceptanceJson)!.Declined!.Single().Spec.Should().Be("s1");
        var request = JsonSerializer.Deserialize<PipelineRequest>(run.QueuedRequestJson!)!;
        ApprovalOf(request.Context![ContextKeys.ApprovedSpecSet].ToString()).Should().Be(SpecApprovalJson.Write(OldShapeJson.Approval));
        var resume = JsonSerializer.Deserialize<ResumePayload>(request.Context[ContextKeys.ResumeCheckpoint].ToString()!)!;
        resume.Commands.First().Name.Should().Be(CommandNames.VerifySpec + ":api");
        resume.ContextJson.Should().Be((await db.RunCheckpoints.AsNoTracking().SingleAsync()).ContextJson);
    }

    private static async Task ReadsDialogAsync(AgentSmithDbContext db)
    {
        var session = await db.SpecDialogSessions.AsNoTracking().SingleAsync();
        OutcomeProposalJson.Read(session.LatestProposalJson!).Should().BeEquivalentTo(
            OutcomeProposalJson.Read(OldShapeJson.Outcome(new SpecOutcome(OldShapeJson.Draft) { Findings = [new ProposalFinding("s1", "p", "w")] })));
        OutcomeProposalJson.Read(session.ConfirmedOutcomeJson!).Should().BeOfType<EpicOutcome>()
            .Which.Children.Single().SpecId.Should().Be("s2");
        SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).Single().Kind.Should().Be(SpecDialogTurnKind.SpecProposal);
        session.LatestFilingJson.Should().Contain("\"kind\":\"spec\"");
        ApprovalOf((await db.ApprovedSeries.AsNoTracking().SingleAsync()).ContentJson).Should().Be(SpecApprovalJson.Write(OldShapeJson.Approval));
    }
}
