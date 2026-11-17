using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Tickets.Body;
using AgentSmith.Server.Services.Tickets.Text;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-dd05b: through the REAL server composition, a filed ticket's body is its spec's region; a person edits the
/// tracker text, and ONE 'ticket-body' run with a run id renders the body from the specs on the branch again and stamps
/// the branch manifest with the fingerprint of the ticket as the tracker now stores it. 2026-11-17-dd05d: the ticket text
/// read names the divergence while the edit stands and none once the body says the specs again.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class TicketBodyActCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task EditedTrackerBody_Overwritten_IsTheSpecsRegionAndTheBranchCarriesItsFingerprint()
    {
        var (tracker, branches, events) = (new BodyTracker(), new RecordingBranchSources(), new ActRunEvents());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new BodyTrackerFactory(tracker));
            services.RemoveAll<ISourceProviderFactory>().AddSingleton<ISourceProviderFactory>(branches);
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
            services.AddTicketReaders(); // GET /api/tickets/.../text, as the dashboard API registers it
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        (await sp.GetRequiredService<IFilingRun>().RunAsync(FilePipelineCompositionTests.State(), Spec(), false, "prop-1"))
            .Filed.Should().ContainSingle();
        tracker.BodyOf("1").Should().Contain(FramedTicketRegion.Begin).And.Contain("Goal of api");
        var texts = sp.GetRequiredService<TicketTextReader>();
        (await texts.ReadAsync(Project, "1", default))!.Divergence.Should().BeNull("the filed body states its spec's goal");
        tracker.Edit("1", "<div><p>Someone rewrote this ticket by hand.</p></div>");
        (await texts.ReadAsync(Project, "1", default, fresh: true))!.Divergence!.Unsaid.Should().Equal("Goal of api");

        var outcome = await sp.GetRequiredService<TicketBodyOverwrite>().OverwriteAsync(Project, "1", "sample.user", default);

        outcome!.Rewritten.Should().BeTrue(outcome.Message);
        tracker.BodyOf("1").Should().StartWith("<div><p>Someone rewrote this ticket by hand.").And.Contain("Goal of api");
        (await texts.ReadAsync(Project, "1", default))!.Divergence.Should().BeNull("the overwrite forgot the cached read");
        var stamped = branches.Writes.Last();
        stamped.Paths.Should().ContainSingle().Which.Should().NotContain(SeriesPaths.Planned, "only the manifest is written; no spec file moves");
        stamped.Files.Single().Content.Should().Contain(TicketTextFingerprint.Of(
            await sp.GetRequiredService<ITicketProviderFactory>().Create(null!).GetTicketAsync(new TicketId("1"), default)));
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.TicketBodyName);
        (await events.ListedRunsAsync(harness.Services)).Single(r => r.Pipeline == PipelinePresets.TicketBodyName)
            .Steps.Select(s => s.CommandName).Should().Contain(CommandNames.RenderTicketBody);
    }

    private static SpecOutcome Spec() =>
        new(new SpecDraft("api-spec", "Goal of api", "spec: api-spec\ngoal: \"Goal of api\"\ndone: [\"api works\"]\n", []) { Done = ["api works"] });
}
