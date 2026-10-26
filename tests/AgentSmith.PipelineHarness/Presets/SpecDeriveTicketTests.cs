using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-26-bdd0b fast-tier end-to-end: a spec derivation carries the ticket id, and a failed one leaves the
/// ticket exactly as it was. The done and failed statuses are seeded, so a finalize that slipped through would
/// move the ticket; the recording provider sees every comment and every status move.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDeriveTicketTests
{
    [Fact]
    public async Task SpecDerive_StepFails_TheTicketIsNeitherCommentedNorMoved()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient.EnqueueThrow(new MasterBudgetExhaustedException("cost budget exhausted"));

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await RunAsync(runner);

        result.Should().NotBeNull("the derivation did not get through");
        tickets.Finalized.Should().BeEmpty("a derivation never moves the ticket, not even when it fails");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains(
            Application.Services.Prompts.OwnTicketComment.WorkingStatus, StringComparison.Ordinal),
            "a derivation is not work on the ticket, so it does not say it is working on it");
        tickets.Commented.Should().NotContain(c => c.Comment.Contains("Failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpecDerive_Lifecycle_NoLabelTransition()
    {
        var tickets = new RecordingTicketProvider();
        var transitions = new RecordingTransitionerFactory();
        await using var harness = BuildHarness(tickets, transitions);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.DerivationJson);

        await RunAsync(new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" });

        transitions.Transitions.Should().BeEmpty("a derivation is not an attempt, so the board does not move");
    }

    // A thrown failure surfaces as the exception, a failed step as the result; both are a derivation that ended.
    private static async Task<CommandResult?> RunAsync(PipelineRunner runner)
    {
        try { return await runner.RunAsync(PipelinePresets.SpecDeriveName); }
        catch (MasterBudgetExhaustedException) { return CommandResult.Fail("thrown"); }
    }

    private static RealCompositionHarness BuildHarness(
        RecordingTicketProvider tickets, RecordingTransitionerFactory? transitions = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            if (transitions is null) return;
            services.RemoveAll<ITicketStatusTransitionerFactory>();
            services.AddSingleton<ITicketStatusTransitionerFactory>(transitions);
        });
}
