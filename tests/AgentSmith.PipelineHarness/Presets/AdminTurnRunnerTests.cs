using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-f3a8a: an admin conversation's turn through the REAL server composition — the conversation runner picks
/// the admin runner for the installation's label, the admin-turn pipeline runs on the installation with the embedded
/// admin master, the reply comes back, and the next turn continues the stored thread.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AdminTurnRunnerTests
{
    private const string Dialog = "d-admin";
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    [Fact]
    public async Task AdminTurn_RunsOnTheInstallation_WithTheAdminMaster()
    {
        await using var harness = BuildHarness();
        var state = await AppendAsync(harness, "how is this installation set up?", open: true);
        harness.ChatClient.EnqueueText("It has one agent and six projects.");

        var result = await TurnAsync(harness, state);

        result.Should().NotBeNull();
        result!.Reply.Should().Contain("one agent");
        harness.ChatClient.CallMessages[0].First(m => m.Role == ChatRole.System).Text
            .Should().Contain(PipelinePresets.AdminAssistantMaster, "the harness stubs every prompt by the name the turn asked for");
        result.RunId.Should().NotBeNullOrEmpty("the turn is a run with an id");
    }

    [Fact]
    public async Task AdminTurn_SecondTurn_ContinuesTheStoredThread()
    {
        await using var harness = BuildHarness();
        var first = await AppendAsync(harness, "remember the word teal", open: true);
        harness.ChatClient.EnqueueText("Noted: teal.");
        await TurnAsync(harness, first);
        var second = await AppendAsync(harness, "which word?");
        var secondStarts = harness.ChatClient.CallMessages.Count;
        harness.ChatClient.EnqueueText("Teal.");

        await TurnAsync(harness, second);

        harness.ChatClient.CallMessages[secondStarts].Should().Contain(m => m.Role == ChatRole.Assistant && m.Text.Contains("Noted: teal."));
    }

    private static async Task<ConversationState> AppendAsync(RealCompositionHarness harness, string text, bool open = false)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        if (open)
            await sessions.OpenAsync(Platform, Dialog, Dialog, "U-admin",
                new ActiveScope { Project = InstallationScope.Name, Repos = [] }, CancellationToken.None);
        return (await sessions.AppendTurnAsync(Platform, Dialog, TranscriptRole.User, text, null, null, CancellationToken.None))!;
    }

    private static async Task<SpecDialogTurnResult?> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U-admin", true), CancellationToken.None);
    }

    internal static RealCompositionHarness BuildHarness(Action<IServiceCollection>? more = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Admin), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            more?.Invoke(services);
        });
}
