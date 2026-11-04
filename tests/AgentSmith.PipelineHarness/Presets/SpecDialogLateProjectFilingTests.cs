using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-04-df50b: a project created in the Config Studio after the server started is filed into.
/// The REAL composition over the REAL config store; the boot-time AgentSmithConfig is resolved first,
/// the way startup resolves it, and only then is the project saved.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogLateProjectFilingTests
{
    private const string LateProject = "late-project";

    [Fact]
    public async Task FileAsync_ProjectSavedAfterStartup_IsFiledInto()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        var booted = harness.Services.GetRequiredService<AgentSmithConfig>();
        SaveProjectCopy(harness.Services, "fixture-fix-bug", LateProject);
        using var scope = harness.Services.CreateScope();

        var report = await scope.ServiceProvider.GetRequiredService<OutcomeTicketFiler>().FileAsync(
            State(), new BugOutcome(new BugTicketDraft("Session drops on refresh", "Users are signed out.", null)),
            mayStartRuns: false, CancellationToken.None);

        booted.Projects.Should().NotContainKey(LateProject, "the boot-time snapshot never learns of it");
        report.Error.Should().BeNull();
        report.Filed.Should().ContainSingle().Which.Project.Should().Be(LateProject);
    }

    // What the studio does when a project is created: one project document saved to the store.
    private static void SaveProjectCopy(IServiceProvider services, string source, string name)
    {
        var store = services.GetRequiredService<IConfigDocumentStore>();
        var doc = store.LoadAll().Single(r => r.Type == ConfigDocTypes.Project && r.Id == source).Doc;
        var edges = services.GetRequiredService<ConfigDocumentAssembler>().EdgesFor(ConfigDocTypes.Project, doc);
        store.Save(new ConfigDocWrite(ConfigDocTypes.Project, name, doc, null, edges, "studio"));
    }

    private static ConversationState State() => new()
    {
        JobId = "s-late", ChannelId = "d-late", ThreadId = "d-late", UserId = "U1", Platform = "dashboard",
        Project = LateProject, TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
    };
}
