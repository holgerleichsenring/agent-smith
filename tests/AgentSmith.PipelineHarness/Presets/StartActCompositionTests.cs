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

/// <summary>
/// 2026-11-17-e061b: a dependent held by its requirement starts once the requirement lands — through the REAL server
/// composition, ONE 'start' run with a run id tags it, and the poller's route then spawns its coding run (the spawn is
/// the harness boundary, as the tracker is). The run list shows the 'start' run with its step.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StartActCompositionTests
{
    private const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequirementsMet_TheHeldDependentOfALandedTicket_StartsInAStartRunAndSpawnsItsRun()
    {
        var (tracker, events, spawn) = (new ActTracker(), new ActRunEvents(), new Mock<ISpawnPipelineRunsUseCase>());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
            services.RemoveAll<ISpawnPipelineRunsUseCase>().AddSingleton(spawn.Object);
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var filed = await sp.GetRequiredService<IFilingRun>().RunAsync(State(), List(), true, "prop-1");
        filed.Filed.Select(f => f.Start!.State).Should().Equal(
            AgentSmith.Application.Services.Filing.FiledStartState.Started, AgentSmith.Application.Services.Filing.FiledStartState.Held);
        await sp.GetRequiredService<ITicketLandingStore>().TryRecordAsync(new TicketLandingFact(
            "github-filing", "github-1", "1", Project, TicketLandingKinds.Merged, Noon, null, null, null), default);
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;

        var end = await harness.Services.GetRequiredService<RequirementsMetStart>().StartAsync(project,
            new ClaimedReworkNudge(Project, "2", ReworkNudgeOrigin.RequirementsMet, null, null, 1, "claim", 0), default);

        end.Should().Be(ReworkNudgeDisposition.Finish);
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.StartName);
        events.Started.Last().RunId.Should().NotBeNullOrEmpty();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.Is<ResolvedProject>(p => p.Name == Project),
            It.IsAny<string>(), It.Is<IncomingTicketEnvelope>(e => e.TicketId == "2"), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
        var listed = await events.ListedRunsAsync(harness.Services);
        listed.Single(r => r.Pipeline == PipelinePresets.StartName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.StartTicket);
    }

    private static TicketGraphOutcome List() => new([Ticket("api", []), Ticket("ui", ["api"])]);

    private static GraphTicket Ticket(string key, IReadOnlyList<string> requires) =>
        new(key, $"Title {key}", $"Requirement of {key}.", [$"{key} works"], requires, [])
        {
            Specs = [new SpecDraft($"{key}-spec", $"Goal of {key}", $"spec: {key}-spec\ngoal: \"Goal of {key}\"\n", [])],
        };

    private static ConversationState State() => new()
    {
        JobId = "s-start", ChannelId = "d-start", ThreadId = "d-start", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "Split it into api and ui.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(1), Decision: SpecDialogDecision.Approved,
                DecisionProposalId: "prop-1"),
        ],
    };
}
