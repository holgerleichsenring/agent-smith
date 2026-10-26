using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-26-bdd0c fast-tier end-to-end: a spec derivation that hands the ticket back asks on the ticket and
/// ends. The clarification status is seeded, so a park that slipped through would move the ticket there.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDeriveQuestionTests
{
    [Fact]
    public async Task SpecDerive_Question_CommentsTheTicketAndEndsWithoutParking()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.QuestionJson);

        var runner = Runner(harness);
        var result = await runner.RunAsync(PipelinePresets.SpecDeriveName);

        Ended(result, runner);
        tickets.Finalized.Should().BeEmpty("a derivation's question leaves the ticket's status where it was");
        tickets.Commented.Should().ContainSingle(c => c.Comment.Contains("derive the specs again", StringComparison.Ordinal),
            "the question is one plain comment on the ticket");
    }

    [Fact]
    public async Task SpecDerive_ScopeRefusal_AsksAndEnds()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient.EnqueueScopeReply(
            "{\"repos\":[{\"name\":\"primary\",\"affected\":true,\"confidence\":0.9}],\"complexity\":\"small\","
            + "\"refusal\":{\"quote\":\"purge the backups\",\"reason\":\"irreversible destruction\"}}");

        var runner = Runner(harness);
        var result = await runner.RunAsync(PipelinePresets.SpecDeriveName);

        Ended(result, runner);
        tickets.Finalized.Should().BeEmpty();
        tickets.Commented.Should().ContainSingle(c => c.Comment.Contains("purge the backups", StringComparison.Ordinal));
        harness.StubSandboxFactory!.Spawned.Should().BeEmpty("a refused derivation ends before checkout");
    }

    private static void Ended(CommandResult result, PipelineRunner runner)
    {
        result.IsSuccess.Should().BeTrue("a derivation's question ends the run; it is no failed step");
        result.Message.Should().StartWith("Spec derivation stopped:");
        runner.LastContext!.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var waiting);
        waiting.Should().BeFalse("no run waits for the answer");
        RunPark.IsWaitingForOperator(runner.LastContext).Should().BeFalse();
    }

    private static PipelineRunner Runner(RealCompositionHarness harness) => new(harness.Services)
    {
        NeedsClarificationStatus = "needs-info", DoneStatus = "done", FailedStatus = "failed",
    };

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
