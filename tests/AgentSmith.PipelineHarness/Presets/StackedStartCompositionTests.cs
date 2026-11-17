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
/// The tracker, the spawn and the source host are the harness's boundary.</summary>
[Trait("Category", "PipelineHarness")]
public sealed class StackedStartCompositionTests
{
    private const string Project = StackedSeries.Project;
    private const string PrerequisitePr = StackedSeries.PrerequisitePr;
    private const string DependentPr = StackedSeries.DependentPr;

    [Fact]
    public async Task GreenPrerequisite_StartsTheDependentStacked_AndItsLandingMovesTheDependentsPullRequest()
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
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;
        var handler = harness.Services.GetRequiredService<ReworkNudgeHandler>();
        await StackedSeries.RunAsync(sp, "1", PrerequisitePr);

        await handler.HandleAsync(project, StackedSeries.Nudge("1", ReworkNudgeOrigin.RunEnd), default);

        nudges.Should().ContainSingle(n => n.TicketId == "2" && n.Origin == ReworkNudgeOrigin.RequirementsMet,
            "the prerequisite's green branch wakes the dependent it holds");
        await handler.HandleAsync(project, StackedSeries.Nudge("2", ReworkNudgeOrigin.RequirementsMet), default);
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.StartName);
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.Is<ResolvedProject>(p => p.Name == Project),
            It.IsAny<string>(), It.Is<IncomingTicketEnvelope>(e => e.TicketId == "2"), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
        var config = new AgentSmithConfig { Projects = new() { [Project] = project } };
        var (claim, refused) = await StackedBaseStamp.ApplyAsync(new ClaimRequest("github", Project, new TicketId("2"), PipelinePresets.CodeName),
            config, sp.GetRequiredService<UnmetTicketRequirements>(), default);
        refused.Should().BeNull();
        claim.InitialContext![ContextKeys.StackedOnTicket].Should().Be("1", "the dependent's run is cut from and targets agent-smith/1");

        await StackedSeries.RunAsync(sp, "2", DependentPr);
        await harness.Services.GetRequiredService<TicketLandingRecorder>().RecordByHandAsync(project, "1", "sample.user", "merged", default);
        await handler.HandleAsync(project, StackedSeries.Nudge("2", ReworkNudgeOrigin.RequirementsMet), default);

        provider.Verify(p => p.RetargetPullRequestAsync(DependentPr, new BranchName("main"), It.IsAny<CancellationToken>()), Times.Once);
        var listed = await events.ListedRunsAsync(harness.Services);
        listed.Single(r => r.Pipeline == PipelinePresets.RetargetName).Steps.Select(s => s.CommandName)
            .Should().Contain(CommandNames.RetargetPullRequests);
    }
}
