using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ee: a design turn on the dashboard tells its dialog what it is DOING, through
/// the REAL server composition — SpecDialogTurnRunner → the spec-dialog preset → the master's
/// own tool surface — with the LLM scripted and the hub recorded.
/// <para>
/// The model calls are not visible here: the harness answers the master from a script rather
/// than through the production chat-client chain, so the decorator that reports them is not in
/// it. Its reporting is proven over the real decorator in ModelCallActivityTests.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogActivityTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-activity";

    private readonly RecordingDialogHub _hub = new();

    [Fact]
    public async Task DialogTurn_OnTheDashboard_PushesWhatItReads()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);

        await RunTurnAsync(harness, state);

        var pushed = Activity();
        pushed.Should().Contain(
            ($"spec-dialog:{Dialog}", Dialog, "tool", "read_file", $"{Repo}/src/Router.cs"),
            "the owner sees which file the turn is on, within seconds of it going there");
        _hub.Pushes.Select(p => p.Group).Distinct().Should().Equal([$"spec-dialog:{Dialog}"],
            "the dialog's group is owner-checked and the run group is not; a step reaches only the first");
    }

    [Fact]
    public async Task DialogTurn_OnSlack_PushesNoActivity()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        var state = await OpenAsync(harness, "slack", "th-activity");

        await RunTurnAsync(harness, state);

        harness.StubSandboxFactory!.Spawned.Should().ContainSingle("the turn did open the repository");
        _hub.Pushes.Should().BeEmpty("a chat thread is a dialog id nobody has joined");
    }

    private IReadOnlyList<(string Group, string DialogId, string Kind, string? Name, string? Detail)> Activity() =>
        [.. _hub.Pushes
            .Where(p => p.Method == "SpecDialogActivity")
            .Select(p => (p.Group, Push: (SpecDialogActivityPush)p.Args[0]!))
            .Select(p => (p.Group, p.Push.DialogId, p.Push.Kind, p.Push.Name, p.Push.Detail))];

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<IHubContext<JobsHub>>();
            services.AddSingleton<IHubContext<JobsHub>>(_hub);
        });

    private static async Task<ConversationState> OpenAsync(
        RealCompositionHarness harness, string platform, string thread)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(platform, thread, thread, "U-activity",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "cut the ordering feature into slices", DateTimeOffset.UtcNow));
    }

    private static async Task RunTurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, CancellationToken.None);
    }
}
