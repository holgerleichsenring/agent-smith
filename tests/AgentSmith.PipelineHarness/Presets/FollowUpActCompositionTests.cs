using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-e061d: a closed ticket's follow-up through the REAL server composition — ONE 'file' run with a run id
/// files the work ticket with no label, and ONE 'comment' run posts the two link comments. The run list shows both.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FollowUpActCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task FollowUp_OfAClosedTicket_FilesInAFileRunAndLinksInACommentRun()
    {
        var (tracker, events) = (new ActTracker(), new ActRunEvents());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var request = new FilingRequest(string.Empty, "carol", [], [], Proposal: null, MayStartRuns: true)
            { FollowUp = new FilingFollowUp("Follow-up: Title 42", "also cover the archive view") };

        var report = await sp.GetRequiredService<AgentSmith.Server.Services.SpecDialog.IFilingRun>().RunRequestAsync(Project, request);
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;
        var missed = await sp.GetRequiredService<FollowUpLink>().PostLinksAsync(project, "42", report.Filed.Single(), default, askedBy: "carol");

        report.Error.Should().BeNull();
        tracker.Created.Should().ContainSingle().Which.Labels.Should().BeEmpty("a follow-up of an act carries no specs, so no stamp");
        missed.Should().BeNull();
        tracker.Comments.Select(c => c.Ticket).Should().Equal("42", "1");
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.CommentName);
        var listed = await events.ListedRunsAsync(harness.Services);
        listed.Single(r => r.Pipeline == PipelinePresets.CommentName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.PostTicketComments);
    }
}
