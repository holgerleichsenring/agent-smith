using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-04-0721: two contexts of one repository that share a toolchain image share a
/// SANDBOX, and only the first of them is that sandbox's representative. Observed live on a
/// Node repository with a `backend` and a `frontend` context: the re-init reported success
/// having bootstrapped `backend` alone, so `frontend` kept the retired principles file and
/// gained no verify block — and the next coding run was refused by a gate that probes both.
/// <para>
/// 2026-09-23-9bb2: the re-init derives again instead of returning what it reads, so the
/// guarantee is now that BOTH contexts reach the round that derives — stated to it from
/// <see cref="ContextKeys.SandboxContexts"/>, which holds every context of the sandbox, and
/// never from the representative map beside it.
/// </para>
/// </summary>
public sealed class ContextsInOneSandboxTests
{
    private const string SandboxKey = "default";
    private const string RepoName = "node-service";

    [Fact]
    public async Task BootstrapDiscover_ReInitOfATwoContextSandbox_StatesBoth()
    {
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"));

        var (prompt, components) = await DiscoverAsync(pipeline);

        prompt.Should().Contain("`backend` — workdir `backend`");
        prompt.Should().Contain("`frontend` — workdir `frontend`",
            "the sibling is in the sandbox's context list and nowhere in the representative map");
        components.Select(c => c.Name).Should().BeEquivalentTo(["backend", "frontend"],
            "one round per context is still what the repository gets");
    }

    [Fact]
    public async Task ReInit_ADeclaredContextWithoutALanguage_TakesItsSandboxs()
    {
        // A context declaring an image but no stack.lang states no language of its own. A
        // sandbox is ONE toolchain, so the group's language is what the round is told it
        // declared — the alternative is an empty slug where a value is being quoted back.
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", null));

        var (prompt, _) = await DiscoverAsync(pipeline);

        prompt.Should().Contain("`frontend` — workdir `frontend`, language `typescript`");
    }

    [Fact]
    public async Task Analyze_TwoContextsInOneSandbox_MapsEachContextsOwnSubtree()
    {
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [SandboxKey] = GitSandbox() });

        var result = await NewAnalyzeHandler().ExecuteAsync(
            new AnalyzeCodeContext(new Repository(new BranchName("main"), "git://x"), pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var byContext = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ProjectMap>>>(
            ContextKeys.ContextProjectMaps)[RepoName];
        byContext.Keys.Should().BeEquivalentTo("backend", "frontend");
        // The analyzer is cached per (sandbox key, workdir), so a map naming the sibling's
        // workdir is a map of the wrong subtree — which is what a round would have written
        // that context.yaml from.
        byContext["frontend"].PrimaryLanguage.Should().Be($"{SandboxKey}@frontend-lang");
        byContext["backend"].PrimaryLanguage.Should().Be($"{SandboxKey}@backend-lang");
        pipeline.Get<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps)[SandboxKey]
            .PrimaryLanguage.Should().Be($"{SandboxKey}@backend-lang", "the per-sandbox map stays the representative's");
    }

    /// <summary>Runs the discovery round and returns (the user prompt it composed, its answer).</summary>
    private static async Task<(string Prompt, IReadOnlyList<DiscoveredComponent> Components)> DiscoverAsync(
        PipelineContext pipeline)
    {
        var chat = new StubChatClient(new Queue<string>([DiscoveryAnswer]));
        var handler = new BootstrapDiscoverHandler(
            new StubChatClientFactory(chat), null, EventTestStubs.RunContext,
            new DiscoveryOutputParser(), new SandboxTargets(),
            new AgentSmith.Application.Services.Tools.AgenticToolSurface(),
            NullLogger<BootstrapDiscoverHandler>.Instance);
        var result = await handler.ExecuteAsync(
            new BootstrapDiscoverContext(RepoName, new AgentConfig(), pipeline), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        chat.InvocationCount.Should().Be(1, "a re-init derives again — it does not read its answer off the tree");
        var prompt = chat.LastMessages.Single(m => m.Role == ChatRole.User).Text ?? string.Empty;
        return (prompt, pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents)[RepoName]);
    }

    private const string DiscoveryAnswer = """
        {
          "status": "complete",
          "components": [
            { "name": "backend",  "workdir": "backend",  "language": "typescript", "evidence": "backend/index.ts" },
            { "name": "frontend", "workdir": "frontend", "language": "typescript", "evidence": "frontend/package.json" }
          ]
        }
        """;

    private static PipelineContext PipelineWithTwoContexts(
        RemoteContextDiscovery representative, RemoteContextDiscovery sibling)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, new[] { new RepoConnection { Name = RepoName, Url = "https://x/y.git", Auth = "t" } });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal) { [SandboxKey] = representative });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [SandboxKey] = [representative, sibling],
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal) { [SandboxKey] = RepoName });
        // 2026-09-23-9bb2: the round is a model call, so it needs the sandbox it reads through,
        // the repository it names, the analysis map it embeds and the skill that carries it.
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles,
            new[] { new RoleSkillDefinition { Name = "project-discovery", OutputSchema = "discovery" } });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [SandboxKey] = Mock.Of<ISandbox>() });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [SandboxKey] = MapFor(SandboxKey) });
        return pipeline;
    }

    private static AnalyzeProjectHandler NewAnalyzeHandler()
    {
        var mapStore = new Mock<IProjectMapStore>();
        mapStore.Setup(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((key, _, _) => Task.FromResult<ProjectMap?>(MapFor(key)));
        return new AnalyzeProjectHandler(
            Mock.Of<IProjectAnalyzer>(), new StubSandboxFileReaderFactory(), mapStore.Object,
            new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
                new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            Mock.Of<IRunArtifactStore>(), new ProjectMapCacheKey(), new SandboxTargets(),
            NullLogger<AnalyzeProjectHandler>.Instance);
    }

    private static ISandbox GitSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "sha-1234567")));
        return sandbox.Object;
    }

    private static ProjectMap MapFor(string cacheKey) => new(
        $"{cacheKey}-lang", [], [], [], [],
        new Conventions(null, null, null), new CiConfig(false, null, null, null));
}
