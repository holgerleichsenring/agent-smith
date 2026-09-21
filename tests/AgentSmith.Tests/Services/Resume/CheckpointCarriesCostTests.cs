using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Resume;

/// <summary>
/// 2026-09-22-7c41a: the checkpoint is the only carrier that works everywhere — a spawned
/// orchestrator has no relational store — so both halves the resume needs ride it: the
/// complexity tier the cap was sized from, and a snapshot of the spend so far.
/// </summary>
public sealed class CheckpointCarriesCostTests
{
    private readonly RecordingEventPublisher _events = EventTestStubs.Recording();

    [Fact]
    public async Task Checkpoint_TheTierAndTheSpendSnapshot_SurviveARoundTrip()
    {
        var pipeline = ParkedRun();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        tracker.Track(Response(input: 700_000, output: 30_000));
        pipeline.Set(ContextKeys.ComplexityTier, ComplexityTier.Large);

        var checkpointed = await Writer().TryCheckpointAsync(
            pipeline, Question(), "job-1", CancellationToken.None);

        checkpointed.Should().BeTrue();
        var restored = new PipelineContext();
        Serializer().Restore(Carried(), restored);
        restored.TryGet<ComplexityTier>(ContextKeys.ComplexityTier, out var tier).Should().BeTrue();
        tier.Should().Be(ComplexityTier.Large);
        restored.TryGet<PriorSpendSnapshot>(ContextKeys.PriorSpend, out var spend).Should().BeTrue();
        spend!.InputTokens.Should().Be(700_000);
        spend.OutputTokens.Should().Be(30_000);
    }

    // The snapshot is a SET, not an accumulate: on a second park the tracker was already
    // seeded from the first, so what it reports is the whole run and adding would double it.
    [Fact]
    public async Task Checkpoint_ASecondPark_OverwritesRatherThanAddsToTheCarriedSnapshot()
    {
        var pipeline = ParkedRun();
        pipeline.Set(ContextKeys.PriorSpend, new PriorSpendSnapshot(
            500_000, 0, 0, 0, 1m, 0, 0, 0, 0, 0m, string.Empty));
        PipelineCostTracker.GetOrCreate(pipeline).Track(Response(input: 600_000, output: 0));

        await Writer().TryCheckpointAsync(pipeline, Question(), "job-1", CancellationToken.None);

        var restored = new PipelineContext();
        Serializer().Restore(Carried(), restored);
        restored.TryGet<PriorSpendSnapshot>(ContextKeys.PriorSpend, out var spend).Should().BeTrue();
        spend!.InputTokens.Should().Be(1_100_000, "the seeded tracker already holds both segments");
    }

    [Fact]
    public async Task Scope_TheEstimatedTier_IsWrittenBesideTheCapItSized()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-7c41a");
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 5m, Tokens = 500_000 });
        var recorder = new ScopeEstimateRecorder(
            new AgentSmithConfig(), _events, NullLogger<ScopeEstimateRecorder>.Instance);

        await recorder.ApplyAsync(
            pipeline, new ScopeEstimate(ComplexityTier.Medium, null), CancellationToken.None);

        pipeline.TryGet<ComplexityTier>(ContextKeys.ComplexityTier, out var tier).Should().BeTrue();
        tier.Should().Be(ComplexityTier.Medium);
    }

    private string Carried() =>
        _events.Events.OfType<RunCheckpointedEvent>().Should().ContainSingle().Subject.ContextJson;

    private DialogueCheckpointWriter Writer() =>
        new(_events, Serializer(), NullLogger<DialogueCheckpointWriter>.Instance);

    private static PipelineContextSerializer Serializer() =>
        new(NullLogger<PipelineContextSerializer>.Instance);

    private static PipelineContext ParkedRun()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-7c41a");
        pipeline.Set(ContextKeys.TicketId, new TicketId("TKT-1"));
        pipeline.Set<IReadOnlyList<PipelineCommand>>(ContextKeys.RemainingCommands, []);
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 30m, Tokens = 15_000_000 });
        return pipeline;
    }

    private static DialogQuestion Question() =>
        new("q-1", QuestionType.FreeText, "which repo?", null, null, null, TimeSpan.FromDays(3));

    private static ChatResponse Response(int input, int output) =>
        new(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = "gpt-4.1",
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output },
        };
}
