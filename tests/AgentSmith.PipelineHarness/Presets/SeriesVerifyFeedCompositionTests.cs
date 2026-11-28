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
/// 2026-11-28-d1ffa: the series check through the REAL server composition over a repository whose nuget.config names the
/// project's private feed. Seen live: the check ran without staged credentials, its restore got NU1301/401, and every
/// criterion that needed a build was filed as failing. Here the restore answers 401 until the user-level NuGet config is
/// written, as the real feed would, so a green restore proves the credentials were staged before the verify master ran it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesVerifyFeedCompositionTests
{
    private const string Project = "fixture-file";
    private const string FeedUrl = "https://pkgs.dev.azure.com/sample-org/_packaging/sample-feed/nuget/v3/index.json";

    [Fact]
    public async Task SeriesCheck_PrivateFeed_RestoresWithStagedCredentials()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.SeriesFeed), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        var sandboxes = harness.StubSandboxFactory!;
        sandboxes.ListedFiles = new(StringComparer.Ordinal) { ["nuget.config"] = NuGetConfig() };
        sandboxes.ShellAnswers["dotnet restore"] = (1, $"error NU1301: Unable to load the service index for source {FeedUrl}. Response status code does not indicate success: 401 (Unauthorized).");
        sandboxes.AnswerAfterWriting("NuGet.Config", "dotnet restore", (0, "Restored /work/filing-fixture/Fixture.csproj"));
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var series = await OpenAsync(sp);
        harness.ChatClient
            .EnqueueToolCall("run_command", """{"command":"dotnet restore","repo":"filing-fixture"}""")
            .EnqueueToolCall("record_criterion", """{"number":1,"holds":true,"evidence":"dotnet restore restored Fixture.csproj"}""")
            .EnqueueToolCall("record_criterion", """{"number":2,"holds":true,"evidence":"dotnet test passed"}""")
            .EnqueueText("2 of 2 hold.");

        var check = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesVerifyName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series.Id });

        check.IsSuccess.Should().BeTrue(check.Message);
        var steps = sandboxes.Spawned.SelectMany(s => s.Sandbox.RanSteps).ToList();
        var staged = steps.FindIndex(s => s.Path == "/root/.nuget/NuGet/NuGet.Config" && s.Content != null
            && s.Content.Contains("harness-feed-token") && s.Content.Contains(FeedUrl));
        staged.Should().BeGreaterThanOrEqualTo(0, "SetupRegistryAuth writes the feed's credentials into the check's sandbox");
        steps.FindIndex(s => s.Command == "/bin/sh" && s.Args?.LastOrDefault() == "dotnet restore")
            .Should().BeGreaterThan(staged, "the credentials are staged before the verify master's restore");
        var messages = harness.ChatClient.CallMessages.SelectMany(m => m).ToList();
        messages.First(m => m.Role == ChatRole.User).Text.Should().Contain("Package-feed credentials staged for you");
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Should().Contain(r => r.Result!.ToString()!.Contains("Restored /work/filing-fixture/Fixture.csproj"));
        var kept = (await sp.GetRequiredService<ISeriesRunStore>().GetAsync(series.Id, default))!;
        kept.Verdicts.Last().Failed.Should().BeEmpty();
        rig.Tracker.Created.Should().HaveCount(2, "nothing failed, so no follow-up is filed");
    }

    private static async Task<SeriesRunRecord> OpenAsync(IServiceProvider sp)
    {
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-feed", "d-feed", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
        ]), true, "prop-1");
        return (await sp.GetRequiredService<ISeriesRunStore>().ActiveForConversationAsync(state.JobId, default))!;
    }

    private static string NuGetConfig() => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
            <add key="sample-feed" value="{FeedUrl}" />
          </packageSources>
        </configuration>
        """;
}
