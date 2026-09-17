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
/// 2026-09-17-c7aec: a design turn on the dashboard tells its dialog which repositories it
/// opens and how each fared, through the REAL server composition — SpecDialogTurnRunner →
/// the spec-dialog preset → the master → the source scopes — with the LLM scripted and the
/// hub recorded.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogReadingTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string TemplateRepo = "spec-dialog-template-fixture";
    private const string PlainProject = "fixture-spec-dialog";
    private const string TemplateProject = "fixture-spec-dialog-template";
    private const string Dialog = "d-reading";

    private readonly RecordingDialogHub _hub = new();

    [Fact]
    public async Task DialogTurn_OnTheDashboard_PushesEachOpenedRepositoryToItsGroup()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, PlainProject, Dialog);

        await RunTurnAsync(harness, state);

        Readings().Should().Equal(
            ($"spec-dialog:{Dialog}", Dialog, Repo, "opening"),
            ($"spec-dialog:{Dialog}", Dialog, Repo, "ready"));
    }

    [Fact]
    public async Task DialogTurn_TemplateScopes_AreReportedToo()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", """{"path": "template:default/src/Api/Order.cs"}""")
            .EnqueueText("Cut along the template's seams.");
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, TemplateProject, Dialog);

        await RunTurnAsync(harness, state);

        Readings().Should().Equal(
            ($"spec-dialog:{Dialog}", Dialog, $"{TemplateRepo}@v1.4.0", "opening"),
            ($"spec-dialog:{Dialog}", Dialog, $"{TemplateRepo}@v1.4.0", "ready"));
    }

    [Fact]
    public async Task DialogTurn_OnSlack_PushesNothing()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        var state = await OpenAsync(harness, "slack", PlainProject, "th-reading");

        await RunTurnAsync(harness, state);

        harness.StubSandboxFactory!.Spawned.Should().ContainSingle("the turn did open the repository");
        _hub.Pushes.Should().BeEmpty("a chat thread is a dialog id nobody has joined");
    }

    private IReadOnlyList<(string Group, string DialogId, string Repo, string State)> Readings() =>
        [.. _hub.Pushes
            .Where(p => p.Method == "SpecDialogReading")
            .Select(p => (p.Group, (SpecDialogReadingPush)p.Args[0]!))
            .Select(p => (p.Group, p.Item2.DialogId, p.Item2.Repo, p.Item2.State))];

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<IHubContext<JobsHub>>();
            services.AddSingleton<IHubContext<JobsHub>>(_hub);
        });

    private static async Task<ConversationState> OpenAsync(
        RealCompositionHarness harness, string platform, string project, string thread)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(platform, thread, thread, "U-reading",
                new ActiveScope { Project = project, Repos = [Repo] }, CancellationToken.None);
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
