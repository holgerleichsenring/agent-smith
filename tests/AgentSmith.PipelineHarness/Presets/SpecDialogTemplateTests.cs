using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-13-ed5a: the design analysis reads the templates this project declares, through
/// the REAL server composition — SpecDialogTurnRunner → the spec-dialog preset → the master
/// — with the LLM scripted.
/// <para>
/// The analysis is the level that decides the SLICES: a feature cut without the house shape
/// produces tickets that cross the layers the template keeps apart, and every run the epic
/// files inherits that cut.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTemplateTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string TemplateProject = "fixture-spec-dialog-template";
    private const string PlainProject = "fixture-spec-dialog";
    private const string Address = "template:default";
    private const string Revision = "v1.4.0";

    [Fact]
    public async Task SpecDialogTurn_ProjectWithTemplates_MaterialisesThemNamed()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Address}}/src/Api/Order.cs"}""")
            .EnqueueText("Three slices, cut along the layers the template keeps apart.");

        var result = await RunTurnAsync(harness, State(TemplateProject));

        result.Reply.Should().Contain("Three slices");
        var spawned = harness.StubSandboxFactory!.Spawned;
        spawned.Should().ContainSingle("only the template was read; the scope's repo was not");
        // 2026-09-22-2d11b: the first step asks the work path who it is a clone of; the
        // clone is the one that names a remote.
        var steps = spawned[0].Sandbox.RanSteps;
        steps.Should().Contain(s => s.Args != null
                && s.Args.Contains("https://stub.test/spec-dialog-template-fixture"),
            "the analysis clones the TEMPLATE's repository, not the target's");
        steps.Should().Contain(s => s.Args != null && s.Args.Contains(Revision),
            "a template is read at the revision the project declared, not at whatever HEAD is");
        FlattenPrompt(harness).Should().Contain(Address)
            .And.Contain("Templates this work is built after",
                "an address the master cannot name is an address it cannot use");
    }

    [Fact]
    public async Task SpecDialogTurn_ProjectWithoutTemplates_SandboxSetUnchanged()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");

        await RunTurnAsync(harness, State(PlainProject));

        var spawned = harness.StubSandboxFactory!.Spawned;
        spawned.Should().ContainSingle("a project with no template addresses what it did before");
        spawned[0].Sandbox.RanSteps.Should().Contain(s => s.Args != null
            && s.Args.Contains("https://stub.test/spec-dialog-fixture"));
        FlattenPrompt(harness).Should().NotContain("template:",
            "a project that declared none is told about none");
    }

    [Fact]
    public async Task SpecDialogTurn_Ends_HoldsTemplateSandboxesForTheNextTurn()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Address}}/src/Api/Order.cs"}""")
            .EnqueueText("Cut along the template's seams.");
        var state = State(TemplateProject);

        await RunTurnAsync(harness, state);

        // 2026-09-22-2d11b: it was disposed until this phase. A template checkout is no
        // longer left running with nobody owning it — the CONVERSATION owns it, the reapers'
        // third rail spares it while the hold window lasts, and a capacity door may evict it.
        harness.StubSandboxFactory!.Spawned.Should().OnlyContain(s => !s.Sandbox.Disposed);
        var held = await harness.Services.GetRequiredService<IHeldSandboxRegister>()
            .TakeAsync(
                HeldSandbox.KeyFor(state.JobId, "spec-dialog-template-fixture", Revision),
                CancellationToken.None);
        held.Should().NotBeNull("the next turn of this conversation reads through it");
    }

    [Fact]
    public async Task SpecDialogTurn_TemplateOpened_StampsTheOutcomeWithWhatItRead()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Address}}/src/Api/Order.cs"}""")
            .EnqueueText("Three slices, cut along the layers the template keeps apart.");

        var result = await RunTurnAsync(harness, State(TemplateProject));

        var template = result.Outcome.Templates.Should().ContainSingle().Subject;
        template.Address.Should().Be(Address);
        template.Repo.Should().Be("spec-dialog-template-fixture");
        template.Opened.Should().BeTrue("the analysis read it, and the filer runs after it is gone");
    }

    // ---- harness plumbing ----

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubSkillsCatalogResolver());
            services.RemoveAll<IProjectMapStore>();
            services.AddSingleton<IProjectMapStore>(new EmptyProjectMapStore());
            // 2026-09-22-2d11b: the stub sandbox runs no agent, so the heartbeat a hold is
            // verified through is answered here.
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
        });

    private static async Task<SpecDialogTurnResult> RunTurnAsync(
        RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>();
        return await runner.RunTurnAsync(state, CancellationToken.None);
    }

    private static ConversationState State(string project) => new()
    {
        JobId = "sess-template",
        ChannelId = "C-template",
        UserId = "U-template",
        Platform = "slack",
        Project = project,
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
        ThreadId = "th-template",
        Transcript = [new TranscriptTurn(
            TranscriptRole.User, "cut the ordering feature into slices", DateTimeOffset.UtcNow)],
        Scope = new ActiveScope { Project = project, Repos = [Repo] },
    };

    private static string FlattenPrompt(RealCompositionHarness harness) =>
        string.Join("\n", harness.ChatClient.LastScriptedMessages.Select(m => m.Text));

    private sealed class StubSkillsCatalogResolver : ISkillsCatalogResolver
    {
        public Task<CatalogResolution> EnsureResolvedAsync(
            SkillsConfig config, CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogResolution(
                "/tmp/agentsmith-harness/empty-catalog", "harness",
                SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
    }

    private sealed class EmptyProjectMapStore : IProjectMapStore
    {
        public Task<ProjectMap?> TryGetAsync(
            string cacheKeyId, string contentHash, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectMap?>(null);

        public Task SetAsync(
            string cacheKeyId, string contentHash, ProjectMap value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
            string cacheKeyPrefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectMap>>([]);
    }
}
