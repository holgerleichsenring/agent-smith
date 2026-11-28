using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-d1ffb: dialogue.question_default_enabled reaches the series master through the REAL server composition. The
/// setting is read from the registered DialogueGlobalConfig — the config store's current dialogue: block in the server — so a
/// change in the Dialogue settings reaches the next beat without a restart. Off, the master's recommended question has no
/// deadline and the chat says it waits.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesQuestionDefaultCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task DefaultsOff_RecommendedQuestion_WaitsWithoutADeadline()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, services =>
            {
                rig.Bind(services);
                services.RemoveAll<DialogueGlobalConfig>().AddTransient(_ => new DialogueGlobalConfig { QuestionDefaultEnabled = false });
            });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-off", "d-off", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
        ]), true, "prop-1");
        var series = (await sp.GetRequiredService<ISeriesRunStore>().ActiveForConversationAsync(state.JobId, default))!;
        await sp.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
            "d-off", "Should forwarding keep the deadline?", "sample.user", true, Project, default);
        harness.ChatClient
            .EnqueueToolCall("ask_human", """{"question":"Keep the original deadline when forwarding?","choices":[{"label":"keep"},{"label":"restart"}],"recommended_index":0}""")
            .EnqueueText("I asked whether forwarding keeps the deadline.");

        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series.Id });

        beat.IsSuccess.Should().BeTrue(beat.Message);
        var kept = (await sp.GetRequiredService<ISeriesRunStore>().GetAsync(series.Id, default))!;
        kept.Question!.Should().Match<SeriesQuestion>(q => q.Recommended == "keep" && q.DeadlineAt == null && !q.NoDefault);
        var texts = (await sp.GetRequiredService<SpecDialogSessionManager>().GetOpenByThreadAsync("dashboard", "d-off", default))!
            .Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text).ToList();
        texts.Should().Contain(t => t.Contains("Nothing is taken by default (dialogue.question_default_enabled is off)"))
            .And.NotContain(t => t.Contains("I take the recommendation at"));
    }
}
