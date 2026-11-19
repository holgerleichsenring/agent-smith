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

    [Fact]
    public async Task Harness_ThreeSpecRun_RowsExistBeforeTheFirstSpecStarts()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), Project);
        harness.ChatClient
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted"));

        var runner = new PipelineRunner(harness.Services).WithSeries([.. HarnessSeries.TwoSpecs,
            new HarnessSeries.Member("Document the guard for callers", "The API notes name the 400 answer.")]);
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
