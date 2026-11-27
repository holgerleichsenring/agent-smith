using AgentSmith.Contracts.Commands;
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
/// 2026-11-27-3532c: the series master reads its project's repository through the REAL server composition — the turn
/// lists the addresses, read_file of &lt;repo&gt;/&lt;path&gt; answers the file at the default branch, and
/// &lt;repo&gt;@&lt;key&gt;/&lt;path&gt; reads a scope checked out at the ticket's branch, agent-smith/&lt;ticket id&gt;.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesMasterSourceReadCompositionTests
{
    private const string Project = "fixture-file";
    private const string Repo = "filing-fixture";
    private const string ApiText = "public sealed class Api { public int Version => 2; }";

    [Fact]
    public async Task ReadFile_DefaultBranch_ReturnsTheRepositoryFile()
    {
        var (harness, scope, series) = await OpenAsync();
        await using var _ = harness;
        await using var __ = scope;
        harness.ChatClient.EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Api.cs"}""").EnqueueText("Read it.");

        await BeatAsync(scope.ServiceProvider, series.Id);

        var messages = harness.ChatClient.CallMessages.SelectMany(m => m).ToList();
        messages.First(m => m.Role == ChatRole.User).Text.Should().Contain($"`{Repo}/` — {Repo} at its default branch")
            .And.Contain($"`{Repo}@{series.Tickets[0].Key}/`");
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Should().Contain(r => r.Result!.ToString()!.Contains(ApiText));
    }

    [Fact]
    public async Task ReadFile_TicketKey_ChecksOutTheTicketsBranch()
    {
        var (harness, scope, series) = await OpenAsync();
        await using var _ = harness;
        await using var __ = scope;
        var ticket = series.Tickets[0];
        harness.ChatClient.EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}@{{ticket.Key}}/src/Api.cs"}""").EnqueueText("Read it.");

        await BeatAsync(scope.ServiceProvider, series.Id);

        harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Should().Contain(s => s.Command == "git" && s.Args != null && s.Args.SequenceEqual(new[] { "checkout", $"agent-smith/{ticket.TicketId}" }));
        harness.ChatClient.CallMessages.SelectMany(m => m).SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Should().Contain(r => r.Result!.ToString()!.Contains(ApiText));
    }

    private static async Task<(RealCompositionHarness, AsyncServiceScope, SeriesRunRecord)> OpenAsync()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        harness.StubSandboxFactory!.SourceFiles = new(StringComparer.Ordinal) { ["src/Api.cs"] = ApiText };
        var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-reads", "d-reads", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
        ]), true, "prop-1");
        var series = (await sp.GetRequiredService<ISeriesRunStore>().ActiveForConversationAsync(state.JobId, default))!;
        rig.Facts.Set(series.Tickets[0].TicketId, "running", finished: false); // a run works it, so it has a branch to read
        return (harness, scope, series);
    }

    // The operator writes to the series, so the master judges this beat.
    private static async Task BeatAsync(IServiceProvider sp, string seriesId)
    {
        await sp.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
            "d-reads", "What does the api's version say?", "sample.user", true, Project, default);
        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = seriesId });
        beat.IsSuccess.Should().BeTrue(beat.Message);
    }
}
