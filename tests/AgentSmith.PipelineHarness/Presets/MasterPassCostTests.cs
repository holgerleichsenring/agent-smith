using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Passes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-13-9a59f: a one-spec run through the production composition, its events projected into the
/// harness store. The master's first pass answers red with its step open — twice, its apply drive included
/// — so the loop re-engages it; the second pass dies on the money fence. Each pass reads its own calls:
/// the stamp the publish path put on every call is what prices the pass.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class MasterPassCostTests
{
    private const string OneSpecJson = """
        {"specs": [
           {"slug": "introduce-the-guard", "goal": "Introduce the empty-payload guard",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400."], "carries": [1,2,3,4]}],
         "discarded": [], "ignored_instructions": [], "handback": {"case": "none", "reason": ""}}
        """;

    private const string Red =
        "Not yet.\n```verdict\n{ \"status\": \"failed\", \"build_ran\": true, \"build_passed\": false, "
        + "\"tests_ran\": false, \"tests_passed\": false, \"summary\": \"the build is red\" }\n```";

    [Fact]
    public async Task Harness_TwoPassSpec_CostPerPassEqualsItsScriptedCalls()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), Project);
        harness.ChatClient
            .EnqueueText(OneSpecJson)
            .EnqueueDeferred(() => Priced(Red))
            .EnqueueDeferred(() => Priced(Red))
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted"));

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");
        var runId = runner.LastRunId!;

        using var scope = harness.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var calls = await uow.Set<RunLlmCall>().AsNoTracking().Where(c => c.RunId == runId && c.MasterPass != null).ToListAsync();
        var view = RunPassesReader.Compose(runId, await RecordsAsync(uow, runId));

        var passes = view.Specs.Should().ContainSingle().Subject.Passes;
        passes.Take(2).Select(p => (p.PassInSpec, p.OpenedBecause, p.EndedBecause, p.ReengageReason)).Should().Equal(
            (1, MasterPassReasons.First, MasterPassReasons.Reengaged, MasterPassReasons.RedWithOpenSteps),
            (2, MasterPassReasons.RedWithOpenSteps, MasterPassReasons.Budget, null));
        passes.Skip(2).Select(p => p.OpenedBecause).Should().StartWith(MasterPassReasons.Repair,
            "the spec's verification hands the outstanding criteria back to a repair master, numbered on");
        passes[0].Calls.Should().Be(2, "the first loop and its apply drive were the first pass");
        passes[0].CostUsd.Should().BeGreaterThan(0m).And.Be(calls.Where(c => c.MasterPass == passes[0].Ordinal).Sum(c => c.CostUsd));
        calls.Should().OnlyContain(c => c.SpecId == view.Specs[0].SpecId);
        view.NotAttributed.Calls.Should().Be(0);
    }

    // The harness has no Run row (nothing projects RunStarted here), so the read is composed over the rows.
    private static async Task<RunPassRecords> RecordsAsync(IUnitOfWork uow, string runId) => new(
        await uow.Set<RunPass>().AsNoTracking().Where(p => p.RunId == runId).ToListAsync(),
        await uow.Set<RunLedgerTransition>().AsNoTracking().Where(t => t.RunId == runId).ToListAsync(),
        await uow.Set<RunLlmCall>().AsNoTracking().Where(c => c.RunId == runId)
            .Select(c => new RunPassCall(c.EventSeq, c.StepIndex, c.SpecId, c.MasterPass, c.CostUsd)).ToListAsync(),
        await uow.Set<RunStep>().AsNoTracking().Where(s => s.RunId == runId)
            .Select(s => new RunPassStep(s.EventSeq, s.StepIndex, s.CommandName)).ToListAsync());

    private static ChatResponse Priced(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { Usage = new UsageDetails { InputTokenCount = 1000, OutputTokenCount = 200 } };

    private static void Project(IServiceCollection services)
    {
        HarnessProjectAnalyzerStub.Register(services);
        services.RemoveAll<IEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new StepAttributingEventPublisher(
            new ProjectingEventPublisher(sp.GetRequiredService<IServiceScopeFactory>()),
            sp.GetRequiredService<IRunContextAccessor>()));
        // The scripted factory publishes no model calls; production's EventPublishingChatClient does.
        var scripted = services.Last(d => d.ServiceType == typeof(IChatClientFactory));
        services.Remove(scripted);
        services.AddSingleton<IChatClientFactory>(sp => new PublishingFactory(
            (IChatClientFactory)scripted.ImplementationFactory!(sp), sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<IRunContextAccessor>()));
    }

    private sealed class PublishingFactory(IChatClientFactory inner, IEventPublisher events, IRunContextAccessor runContext)
        : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) =>
            new EventPublishingChatClient(inner.Create(agent, task, maxIterations, masterLoopHooks), events, runContext,
                new LlmCallCostCalculator(new ModelPricingResolver()), new ThrottleWaitReporter(), "gpt-4o");

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => inner.GetMaxOutputTokens(agent, task);
        public string GetModel(AgentConfig agent, TaskType task) => inner.GetModel(agent, task);
    }
}
