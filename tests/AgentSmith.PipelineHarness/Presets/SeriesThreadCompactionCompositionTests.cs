using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-25-433fb: a series master's thread over days, through the REAL server composition — a long stored thread whose
/// first beat holds a decision passes dialogue.series_master_thread_max_chars; the beat's last step has the master write its
/// notes in place of its older beats, and the next beat's model call reads those notes, with the decision, and not the
/// beats they replaced.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesThreadCompactionCompositionTests
{
    private const string Project = "fixture-file";
    private const string Decision = "Decided: #2 merges before #3, because the ui reads the api's new field.";
    private const string Notes = "Notes. " + Decision + " Open: nothing. Plan: merge #2 once green.";

    [Fact]
    public async Task SeriesMaster_ALongThread_CompactsIntoNotes_TheNextBeatSeesTheDecision()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var dialogue = sp.GetRequiredService<AgentSmithConfig>().Dialogue;
        (dialogue.SeriesMasterThreadMaxChars, dialogue.SeriesMasterThreadKeepTurns) = (3000, 2);
        var (store, series) = await OpenSeriesAsync(sp);
        await store.UpdateAsync(series.Id, r => r with { ThreadJson = LongThread() }, default);

        harness.ChatClient.EnqueueText("Noted, #2 is still coding.").EnqueueText(Notes);
        await BeatAsync(sp, series.Id, "How is it going?");

        var kept = SeriesThread.Read((await store.GetAsync(series.Id, default))!.ThreadJson);
        kept.Select(m => m.Text).Take(2).Should().Equal(SeriesThread.NotesHead, Notes);
        kept.Should().HaveCount(6, "the notes pair and the latest two beats, the newest being this one");
        kept[^2].Text.Should().Contain("How is it going?").And.NotContain("| ticket |", "a stored turn carries no table");

        var before = harness.ChatClient.CallMessages.Count;
        var notesCall = string.Join("\n", harness.ChatClient.CallMessages[before - 1].Select(m => m.Text));
        notesCall.Should().Contain("filler beat 1").And.Contain(Decision).And.Contain(CompactSeriesThreadHandler.Instruction);
        harness.ChatClient.EnqueueText("Still on plan.");
        await BeatAsync(sp, series.Id, "Anything new?");

        var seen = string.Join("\n", harness.ChatClient.CallMessages[before].Select(m => m.Text));
        seen.Should().Contain(Decision, "the master still sees what it decided").And.NotContain("filler beat 1");
    }

    private static async Task<(ISeriesRunStore Store, SeriesRunRecord Series)> OpenSeriesAsync(IServiceProvider sp)
    {
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-thread", "d-thread", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
            FilePipelineCompositionTests.Ticket("docs", ["api"]),
        ]), true, "prop-1");
        var store = sp.GetRequiredService<ISeriesRunStore>();
        return (store, (await store.ActiveForConversationAsync(state.JobId, default))!);
    }

    // The operator writes to the series, so the master judges this beat.
    private static async Task BeatAsync(IServiceProvider sp, string seriesId, string message)
    {
        await sp.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync("d-thread", message, "sample.user", true, Project, default);
        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = seriesId });
        beat.IsSuccess.Should().BeTrue(beat.Message);
    }

    // Six earlier beats of about 600 characters; the first one holds the decision.
    private static string LongThread() => Enumerable.Range(1, 6).Aggregate("[]", (json, i) => SeriesThread.Append(json,
        $"filler beat {i}: " + new string('x', 600), i == 1 ? Decision : $"answer {i}"));
}
