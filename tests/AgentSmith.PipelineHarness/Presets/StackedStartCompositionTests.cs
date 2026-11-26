using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-11-17-9a39b/c, real server composition: the prerequisite's green run wakes the held dependent, which starts
/// stacked on agent-smith/1; once the prerequisite lands, ONE 'retarget' run moves the dependent's pull request to main.
/// The tracker, the spawn and the source host are the harness's boundary. 2026-11-25-9992e: while the series is active its
/// master owns the retarget, so the nudge moves nothing; once the series stopped the ticket is standalone again.</summary>
[Trait("Category", "PipelineHarness")]
public sealed class StackedStartCompositionTests
{
    private const string Project = StackedSeries.Project;
    private const string PrerequisitePr = StackedSeries.PrerequisitePr;
    private const string DependentPr = StackedSeries.DependentPr;

    [Fact]
    public async Task GreenPrerequisite_WakesTheDependentsMaster_WhichStartsItStacked()
    {
        var (tracker, events, spawn, nudges) = (new ActTracker(), new ActRunEvents(), new Mock<ISpawnPipelineRunsUseCase>(), new List<ReworkNudgeRequest>());
        var provider = StackedSeries.Host();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
            services.RemoveAll<ISpawnPipelineRunsUseCase>().AddSingleton(spawn.Object);
            services.RemoveAll<ISourceProviderFactory>().AddSingleton(Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == provider.Object));
            services.RemoveAll<IReworkNudges>().AddSingleton(StackedSeries.Recording(nudges));
            // The series runway reads the ticket's branch, which this harness does not compose; the run end's tail is not under test.
            services.RemoveAll<AgentSmith.Server.Contracts.ISeriesAutoStart>().AddSingleton(Mock.Of<AgentSmith.Server.Contracts.ISeriesAutoStart>(a =>
                a.ContinueAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(ReworkNudgeDisposition.Finish)));
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IFilingRun>().RunAsync(StackedSeries.State(), StackedSeries.List(), true, "prop-1");
        await AsOpenedBeforeSeriesBranchesAsync(sp);
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;
        var handler = harness.Services.GetRequiredService<ReworkNudgeHandler>();
        await StackedSeries.RunAsync(sp, "1", PrerequisitePr);

        await handler.HandleAsync(project, StackedSeries.Nudge("1", ReworkNudgeOrigin.RunEnd), default);

        // 2026-11-25-96aea: the green branch wakes the series master holding the dependent; nothing starts by itself
        var store = sp.GetRequiredService<AgentSmith.Contracts.Series.ISeriesRunStore>();
        var series = (await store.ActiveAsync(default)).Should().ContainSingle().Subject;
        series.Wakes.Should().ContainSingle(w => w.TicketId == "2" && w.Kind == AgentSmith.Contracts.Series.SeriesWakeKinds.Prerequisite,
            "the prerequisite's green branch is the master's to act on");
        nudges.Should().NotContain(n => n.Origin == ReworkNudgeOrigin.RequirementsMet);
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName);
        var config = new AgentSmithConfig { Projects = new() { [Project] = project } };
        var (claim, refused) = await StackedBaseStamp.ApplyAsync(new ClaimRequest("github", Project, new TicketId("2"), PipelinePresets.CodeName),
            config, sp.GetRequiredService<UnmetTicketRequirements>(), default);
        refused.Should().BeNull();
        claim.InitialContext![ContextKeys.StackedOnTicket].Should().BeEquivalentTo(new Dictionary<string, string> { ["filing-fixture"] = "1" },
            "the dependent's run, once its master starts it, is cut from and targets agent-smith/1 (2026-11-23-31dca)");
    }

    // 2026-11-24-f9c5b: a series filed now integrates on its series branch and never stacks; stacking stays for a series
    // opened before that (no branch), which is what this composition still proves.
    private static async Task AsOpenedBeforeSeriesBranchesAsync(IServiceProvider sp)
    {
        var store = sp.GetRequiredService<AgentSmith.Contracts.Series.ISeriesRunStore>();
        foreach (var series in await store.ActiveAsync(default))
            await store.UpdateAsync(series.Id, r => r with { IntegrationBranch = null }, default);
    }
}
