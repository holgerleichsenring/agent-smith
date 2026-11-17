using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Editing;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-dd05c: through the REAL server composition, a person edits the one spec of a filed, never-worked ticket
/// from its page: ONE 'amend' run with a run id writes the edited spec to the branch, records it and renders the body
/// from it — the way an approval in the ticket's conversation goes. An invalid yaml and an unknown project record nothing.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class TicketSpecEditCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task NeverWorkedTicket_SpecEdited_AmendRunWritesBranchRecordAndBody()
    {
        var (tracker, branches, events) = (new BodyTracker(), new RecordingBranchSources(), new ActRunEvents());
        await using var harness = Build(tracker, branches, events);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await FileAsync(sp);
        var spec = await FiledSpecAsync(sp);

        var answer = await sp.GetRequiredService<TicketSpecEdit>().EditAsync(
            Project, "1", spec.SpecId, spec.Yaml.Replace("Goal of api", "Goal of api, edited", StringComparison.Ordinal), "sample.user", default);

        answer.Status.Should().Be(StatusCodes.Status200OK, answer.Message);
        (await FiledSpecAsync(sp)).Goal.Should().Be("Goal of api, edited", "the branch holds the edit");
        tracker.BodyOf("1").Should().Contain("Goal of api, edited", "the body is rendered from the edited spec");
        var record = await sp.GetRequiredService<ISpecApprovalStore>().GetAsync("github-filing", "github-1", default);
        (record!.Set.Approval!.Principal, record.Set.Specs.Single().Draft.Goal).Should().Be(("sample.user", "Goal of api, edited"));
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.AmendName);
    }

    [Fact]
    public async Task InvalidYamlOrUnknownProject_RecordNothing()
    {
        var (tracker, branches, events) = (new BodyTracker(), new RecordingBranchSources(), new ActRunEvents());
        await using var harness = Build(tracker, branches, events);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await FileAsync(sp);
        var spec = await FiledSpecAsync(sp);
        var edit = sp.GetRequiredService<TicketSpecEdit>();

        (await edit.EditAsync(Project, "1", spec.SpecId, "goal: [unclosed", "sample.user", default)).Status.Should().Be(StatusCodes.Status409Conflict);
        (await edit.EditAsync("no-such-project", "1", spec.SpecId, spec.Yaml, "sample.user", default)).Status.Should().Be(StatusCodes.Status404NotFound);
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName);
    }

    private static RealCompositionHarness Build(BodyTracker tracker, RecordingBranchSources branches, ActRunEvents events) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new BodyTrackerFactory(tracker));
            services.RemoveAll<ISourceProviderFactory>().AddSingleton<ISourceProviderFactory>(branches);
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
        });

    private static async Task FileAsync(IServiceProvider sp) =>
        (await sp.GetRequiredService<IFilingRun>().RunAsync(FilePipelineCompositionTests.State(), new SpecOutcome(
            new SpecDraft("api-spec", "Goal of api", "spec: api-spec\ngoal: \"Goal of api\"\ndone: [\"api works\"]\n", []) { Done = ["api works"] }),
            false, "prop-1")).Filed.Should().ContainSingle();

    private static async Task<SpecDraft> FiledSpecAsync(IServiceProvider sp)
    {
        var project = sp.GetRequiredService<TicketProjects>().Of(Project)!;
        return (await sp.GetRequiredService<NextAttemptSeriesReader>().ReadAsync(project, "1", false, default)).Specs.Single();
    }
}
