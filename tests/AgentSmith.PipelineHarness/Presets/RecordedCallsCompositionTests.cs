using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-7c9bc through the production composition: a code run whose every model call is answered the
/// way the worker bridge answers (the CLI's own cost on the response). The run's call ledger holds exactly
/// the calls the projection recorded, at the same cost — while the cost tracker, which leaves worker calls
/// out, says nothing was spent.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class RecordedCallsCompositionTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed"}""";

    [Fact]
    public async Task Harness_WorkerAnsweredRun_LedgerMatchesTheRecordedCalls()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), Project);
        harness.ChatClient
            .EnqueueDeferred(() => Worker(SpecDerivationFixture.DerivationJson, 0.25m))
            .EnqueueDeferred(() => Worker("Planning: patch it.", 0.50m))
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// fix"}""")
            .EnqueueDeferred(() => Worker(GreenVerdict, 0.75m));

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var ledger = runner.LastContext!.Get<RecordedCallLedger>(ContextKeys.RecordedCalls);
        using var scope = harness.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunLlmCall>().AsNoTracking()
            .Where(c => c.RunId == runner.LastRunId).ToListAsync();
        ledger.Calls.Should().HaveCount(rows.Count).And.NotBeEmpty();
        ledger.CostUsd.Should().Be(rows.Sum(r => r.CostUsd)).And.BeGreaterThanOrEqualTo(1.50m);
        PipelineCostTracker.GetOrCreate(runner.LastContext!).EstimateCostUsd().Should().Be(0m,
            "the tracker keeps worker spend apart; the ledger is what the run recorded");
    }

    private static ChatResponse Worker(string text, decimal reportedUsd) =>
        new WorkerCallAccounting("claude-opus-5-5", 10, 20, 0, 0, reportedUsd, 1)
            .AttachTo(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

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
                new LlmCallCostCalculator(new ModelPricingResolver()), new ThrottleWaitReporter(), "claude-opus-5-5");

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => inner.GetMaxOutputTokens(agent, task);
        public string GetModel(AgentConfig agent, TaskType task) => inner.GetModel(agent, task);
    }
}
