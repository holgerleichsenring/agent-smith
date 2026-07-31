using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0388a's done-bullet, proven against a REAL pipeline run instead of a mocked
/// step runner: a scripted fix-bug run through the production composition must
/// persist rows whose step attribution matches the steps that actually ran.
///
/// <para>Covers both emission shapes the ambient step scope exists for — an event
/// raised on the step handler's own flow (LoadCatalog's CatalogLoaded) and one
/// raised from a nested child task (SubAgentSpawned, published inside
/// SubAgentRunner's Task.WhenAll fan-out). If the AsyncLocal frame did not flow
/// to children the second would persist unattributed, and if the stamp were a
/// constant the two would not differ.</para>
///
/// <para>Fast tier: StubSandbox, scripted LLM, SQLite file. No Docker, no Redis,
/// no GITHUB_TOKEN.</para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StepAttributionTests
{
    // One child only: the sub-agent's loop pulls from the SAME scripted FIFO as
    // the master, so a wider fan-out would make the queue order racy.
    private const string SpawnOneSubAgent =
        """
        {"tasks":[{"name":"contract-scout","activity":"reading the contract",
        "task_description":"Report the interface the fix must honour.",
        "inherited_context":{"pipeline_goal":"fix the bug","prior_context_slice":"none"}}]}
        """;

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[]}""";

    [Fact]
    public async Task RealRun_EventFromStepHandlerAndFromChildTask_PersistWithTheirOwnStepIndex()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            // p0390: DeriveSpecification runs between NegotiateExpectation and
            // GeneratePlan and drains one FIFO slot.
            .EnqueueText(WorkSpecDerivationTests.SpecJson)  // NegotiateExpectation
            .EnqueueText("Planning: I will patch the file.")     // GeneratePlan
            .EnqueueToolCall("spawn_agents", SpawnOneSubAgent)   // master → child task
            .EnqueueText("Scout: the interface is IPatch.")      // the child's own turn
            .EnqueueText(GreenVerdict);

        var rows = await RunAndReadAsync(harness);

        // The child task's event inherited the step that spawned it.
        var spawnedIn = rows.StepIndexOf(EventType.SubAgentSpawned);
        spawnedIn.Should().NotBeNull(
            "a sub-agent event raised on a child task must inherit the spawning step — "
            + "a null here means the AsyncLocal step frame did not flow to the child");
        rows.StepAt(spawnedIn!.Value)?.CommandName.Should().Be(CommandNames.AgenticMaster,
            "the spawning step is the master's, and that is the step the child's event names");

        // An event the handler raised on the step's own flow, in a DIFFERENT step.
        var catalogLoadedIn = rows.StepIndexOf(EventType.CatalogLoaded);
        catalogLoadedIn.Should().NotBeNull("a handler-raised event carries its own step too");
        catalogLoadedIn.Should().NotBe(spawnedIn,
            "attribution must discriminate between steps — one constant stamp would pass "
            + "every other assertion here while telling the operator nothing");
    }

    [Fact]
    public async Task RealRun_EveryAttributedRow_NamesAStepThatActuallyRan()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueText(ExpectationNegotiationTests.DraftJson)
            // p0390: DeriveSpecification runs between NegotiateExpectation and
            // GeneratePlan and drains one FIFO slot.
            .EnqueueText(WorkSpecDerivationTests.SpecJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        var rows = await RunAndReadAsync(harness);

        rows.Steps.Should().NotBeEmpty("the run must have executed steps to attribute against");
        var ran = rows.Steps.Select(s => s.StepIndex).ToHashSet();
        rows.AttributedTrailIndices.Should().NotBeEmpty(
            "a real run publishes events inside its steps, so attribution must be present");
        rows.AttributedTrailIndices.Should().OnlyContain(i => ran.Contains(i),
            "attribution is producer knowledge — no row may name a step the run never ran");
        // The typed child projections carry it too, not just the raw trail.
        rows.Sandboxes.Where(s => s.StepIndex is not null)
            .Should().OnlyContain(s => ran.Contains(s.StepIndex!.Value));
    }

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), RegisterAttributedProjection);

    // The harness swaps the production IEventPublisher (Redis) for a NoOp, which
    // takes the step-attributing decorator with it. Put BOTH halves back: the
    // production decorator over a publisher that projects into the harness DB, so
    // what this test reads was written by the real stamping + projection code.
    private static void RegisterAttributedProjection(IServiceCollection services)
    {
        // The production LLM-driven analyzer would drain the scripted FIFO at
        // AnalyzeCode and steal the master's turns (the same reason the keystone
        // tests stub it), so the run would never reach the master at all.
        HarnessProjectAnalyzerStub.Register(services);
        services.RemoveAll<IEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new StepAttributingEventPublisher(
            new ProjectingEventPublisher(sp.GetRequiredService<IServiceScopeFactory>()),
            sp.GetRequiredService<IRunContextAccessor>()));
    }

    private static async Task<PersistedRun> RunAndReadAsync(RealCompositionHarness harness)
    {
        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("fix-bug");
        var runId = runner.LastRunId!;
        using var scope = harness.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new PersistedRun(
            await uow.Set<RunStep>().AsNoTracking().Where(s => s.RunId == runId).ToListAsync(),
            await uow.Set<Infrastructure.Persistence.Entities.RunEvent>().AsNoTracking()
                .Where(e => e.RunId == runId).ToListAsync(),
            await uow.Set<RunSandbox>().AsNoTracking().Where(s => s.RunId == runId).ToListAsync());
    }

    private sealed record PersistedRun(
        IReadOnlyList<RunStep> Steps,
        IReadOnlyList<Infrastructure.Persistence.Entities.RunEvent> Trail,
        IReadOnlyList<RunSandbox> Sandboxes)
    {
        public IReadOnlyList<int> AttributedTrailIndices =>
            Trail.Where(e => e.StepIndex is not null).Select(e => e.StepIndex!.Value).ToList();

        public int? StepIndexOf(EventType type) =>
            Trail.FirstOrDefault(e => e.Type == type.ToString())?.StepIndex;

        public RunStep? StepAt(int stepIndex) => Steps.FirstOrDefault(s => s.StepIndex == stepIndex);
    }
}
