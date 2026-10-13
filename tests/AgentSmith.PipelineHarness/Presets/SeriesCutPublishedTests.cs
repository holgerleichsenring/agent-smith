using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-13-9a59e: a three-spec derivation through the production composition, its events projected
/// into the harness store. The cut lands before the first spec starts and stands every spec up as a
/// row, so a run's specs are readable from the moment it is derived. The first master dies on the
/// money fence, which leaves the two later specs exactly as the cut wrote them.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesCutPublishedTests
{
    private const string ThreeSpecJson = """
        {"specs": [
           {"slug": "introduce-the-guard", "goal": "Introduce the empty-payload guard",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400."], "carries": [1,2,3,4]},
           {"slug": "migrate-the-callers", "goal": "Move the existing callers onto the guard",
            "steps": [{"id": "callers", "action": "Route every caller through the guard"}],
            "done": ["No caller builds its own empty-payload check."], "carries": [5,6,7,8]},
           {"slug": "document-the-guard", "goal": "Document the guard for callers",
            "steps": [{"id": "docs", "action": "Describe the 400 answer in the API notes"}],
            "done": ["The API notes name the 400 answer."], "carries": [9,10,11,12]}],
         "discarded": [], "ignored_instructions": [], "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public async Task Harness_ThreeSpecRun_RowsExistBeforeTheFirstSpecStarts()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), Project);
        harness.ChatClient
            .EnqueueText(ThreeSpecJson)
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted"));

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");
        var runId = runner.LastRunId!;

        using var scope = harness.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var cut = await uow.Set<RunSeriesCut>().AsNoTracking().SingleAsync(c => c.RunId == runId);
        var firstStart = await uow.Set<Infrastructure.Persistence.Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId && e.Type == nameof(EventType.SpecStateChanged)).MinAsync(e => e.Seq);
        var specs = await uow.Set<RunSpec>().AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.Ordinal).ToListAsync();

        cut.EventSeq.Should().BeLessThan(firstStart, "the cut is published before any spec starts");
        specs.Select(s => s.Ordinal).Should().Equal(1, 2, 3);
        specs[0].StartedAt.Should().NotBeNull("the first spec started");
        specs.Skip(1).Should().OnlyContain(s => s.Status == "not_started" && s.StartedAt == null,
            "the cut stood the later specs up before anything ran them");
        specs[2].Title.Should().Be("Document the guard for callers");
    }

    private static void Project(IServiceCollection services)
    {
        HarnessProjectAnalyzerStub.Register(services);
        services.RemoveAll<IEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => new StepAttributingEventPublisher(
            new ProjectingEventPublisher(sp.GetRequiredService<IServiceScopeFactory>()),
            sp.GetRequiredService<IRunContextAccessor>()));
    }
}
