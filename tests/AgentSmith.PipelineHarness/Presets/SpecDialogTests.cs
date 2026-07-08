using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0315b: the design-partner turn end-to-end through the REAL server
/// composition — SpecDialogTurnRunner → ExecutePipelineUseCase →
/// spec-dialog preset → AgenticMasterHandler — with the LLM scripted.
/// Tier-1 (cached code map, no sandbox), tier-2 (lazy read-only source
/// sandbox on the first content read), and the phase-spec draft gate
/// (valid shown, invalid re-prompted and never surfaced raw).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTests
{
    private const string Project = "fixture-spec-dialog";
    private const string Repo = "spec-dialog-fixture";

    private const string ValidDraft =
        """
        ```yaml
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
        ```
        """;

    [Fact]
    public async Task DesignPartner_StructuralQuestion_AnsweredFromCodeMap_NoSandbox_NoArtifact()
    {
        await using var harness = BuildHarness();
        const string answer = "Dispatch flows through the intent engine into the per-intent handlers.";
        harness.ChatClient.EnqueueText(answer);

        var reply = await RunTurnAsync(harness, State("how does message dispatch work?"));

        reply.Should().Be(answer);
        reply.Should().NotContain("```yaml", "a grounded answer carries no artifact");
        harness.StubSandboxFactory!.Spawned.Should().BeEmpty(
            "a structural question is answered from the cached code map — no source sandbox spins up");
        FlattenPrompt(harness).Should().Contain("primary_language: csharp",
            "the cached ProjectMap is the tier-1 grounding the master sees");
    }

    [Fact]
    public async Task DesignPartner_ContentQuestion_SpinsReadOnlySandbox()
    {
        await using var harness = BuildHarness();
        const string answer = "AppendTurnAsync persists the turn, then the router replies threaded.";
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText(answer);

        var reply = await RunTurnAsync(harness, State("what exactly does AppendTurnAsync do?"));

        reply.Should().Be(answer);
        var spawned = harness.StubSandboxFactory!.Spawned;
        spawned.Should().HaveCount(1, "the first content read materialises exactly one read-only sandbox");
        spawned[0].Spec.ToolchainImage.Should().Be("buildpack-deps:bookworm-scm",
            "the source sandbox uses the generic git-bearing image — no toolchain resolution");
        spawned[0].Sandbox.RanSteps.Select(s => s.Kind).Should().ContainInOrder(
            StepKind.Run, StepKind.ReadFile);
        spawned[0].Sandbox.RanSteps[0].Command.Should().Be("git", "materialisation clones the scoped repo");
    }

    [Fact]
    public async Task DesignPartner_PhaseOutcome_ProducesSchemaValidDraft()
    {
        await using var harness = BuildHarness();
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}");

        var reply = await RunTurnAsync(harness, State("draft the widget phase now"));

        reply.Should().Contain("phase: p9999", "a schema-valid draft is shown as-is");
        harness.ChatClient.InvocationCount.Should().Be(1, "a valid draft needs no re-prompt");
    }

    [Fact]
    public async Task DesignPartner_InvalidDraft_Reprompted_NotSurfacedRaw()
    {
        await using var harness = BuildHarness();
        harness.ChatClient
            .EnqueueText("Draft:\n```yaml\nphase: not-a-valid-phase-id\n```")
            .EnqueueText($"Corrected:\n{ValidDraft}");

        var reply = await RunTurnAsync(harness, State("draft the widget phase now"));

        harness.ChatClient.InvocationCount.Should().Be(2,
            "the invalid draft re-prompts the master exactly once with the schema error");
        reply.Should().Contain("phase: p9999", "the corrected draft is what surfaces");
        reply.Should().NotContain("not-a-valid-phase-id", "the invalid draft is never surfaced raw");
        FlattenPrompt(harness).Should().Contain("failed schema validation",
            "the re-prompt names the validation failure to the master");
    }

    // ---- harness plumbing ----

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            // The turn runner goes through ExecutePipelineUseCase, which resolves
            // the skills catalog — stub it (network boundary, like the harness
            // stubs the catalog path itself).
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubSkillsCatalogResolver());
            // Tier-1 grounding source: a canned cached ProjectMap (the production
            // store is Redis; the fast tier has none).
            services.RemoveAll<IProjectMapStore>();
            services.AddSingleton<IProjectMapStore>(new CannedProjectMapStore(CannedMap()));
            // The stub prompt catalog carries no tokens; spec-dialog asserts the
            // cached code map reaches the master, so render {CodeMapSection}.
            services.RemoveAll<IPromptCatalog>();
            services.AddSingleton<IPromptCatalog>(new TokenRenderingPromptCatalog(
                "You are the design partner for this test.\n{CodeMapSection}"));
        });

    private static async Task<string> RunTurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>();
        return await runner.RunTurnAsync(state, CancellationToken.None);
    }

    private static ConversationState State(string userTurn) => new()
    {
        JobId = "sess-harness",
        ChannelId = "C-harness",
        UserId = "U-harness",
        Platform = "slack",
        Project = Project,
        TicketId = 0,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
        ThreadId = "th-harness",
        Transcript = [new TranscriptTurn(TranscriptRole.User, userTurn, DateTimeOffset.UtcNow)],
        Scope = new ActiveScope { Project = Project, Repos = [Repo] },
    };

    private static ProjectMap CannedMap() => new(
        "csharp", ["net8"],
        [new Module("src", ModuleRole.Production, [])],
        [], [], new Conventions(null, null, null),
        new CiConfig(false, null, null, null));

    private static string FlattenPrompt(RealCompositionHarness harness) =>
        string.Join("\n", harness.ChatClient.LastMessages.Select(m => m.Text));

    private sealed class StubSkillsCatalogResolver : ISkillsCatalogResolver
    {
        public Task<CatalogResolution> EnsureResolvedAsync(
            SkillsConfig config, CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogResolution(
                "/tmp/agentsmith-harness/empty-catalog", "harness",
                SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
    }

    private sealed class CannedProjectMapStore(ProjectMap map) : IProjectMapStore
    {
        public Task<ProjectMap?> TryGetAsync(
            string cacheKeyId, string contentHash, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectMap?>(null);

        public Task SetAsync(
            string cacheKeyId, string contentHash, ProjectMap value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
            string cacheKeyPrefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectMap>>([map]);
    }

    private sealed class TokenRenderingPromptCatalog(string body) : IPromptCatalog
    {
        public string Get(string name) => body;

        public string Render(string name, IReadOnlyDictionary<string, string> tokens)
        {
            var rendered = body;
            foreach (var (key, value) in tokens)
                rendered = rendered.Replace("{" + key + "}", value, StringComparison.Ordinal);
            return rendered;
        }
    }
}
