using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-23-bb73: which analysis map a bootstrap round reads — the successor defect to
/// 2026-09-23-6698, one line from the sandbox resolution it fixed.
/// <para>
/// <see cref="ContextKeys.RepoProjectMaps"/> is published under the composed SANDBOX KEY, and
/// both rounds looked it up by REPOSITORY NAME, bounding the miss with a COUNT — the sole entry
/// in discovery, the sole entry of a single-sandbox run in the round. A repository with several
/// toolchain groups keys on the context name or on repo-plus-context, so the name missed, the
/// count refused the fallback, and the round failed with no map at all. Where the count DID
/// allow it, the entry it handed over could belong to another repository.
/// </para>
/// <para>
/// The fixtures kept it invisible: they keyed the maps by repository name, which production
/// never does for a multi-group repository, so every test passed over a dead resolution.
/// </para>
/// </summary>
public sealed class BootstrapProjectMapResolutionTests
{
    private static readonly RoleSkillDefinition DiscoverySkill = new()
    {
        Name = "project-discovery",
        DisplayName = "Project Discovery",
        Description = "test",
        Emoji = "🔍",
        Rules = "test",
        Role = "producer",
        OutputSchema = "discovery",
    };

    private static readonly RoleSkillDefinition BootstrapSkill = new()
    {
        Name = "project-bootstrap",
        DisplayName = "Project Bootstrap",
        Description = "test",
        Emoji = "🔧",
        Rules = "test",
        Role = "producer",
        OutputSchema = "bootstrap",
    };

    private const string DiscoveryAnswer = """
        {
          "status": "complete",
          "components": [
            { "name": "default", "workdir": ".", "language": "csharp", "evidence": "src/Program.cs" }
          ]
        }
        """;

    [Fact]
    public async Task BootstrapDiscover_MultiGroupRepo_ResolvesItsOwnProjectMap()
    {
        // Two repositories, the first with two toolchain groups: NO map key equals a repository
        // name, so the name lookup missed every one of them and the three-entry dictionary
        // refused the sole-entry fallback — a hard "no ProjectMap available for repo".
        var prompts = new List<string>();
        var pipeline = DiscoverPipeline();

        var result = await NewDiscoverHandler(prompts).ExecuteAsync(
            new BootstrapDiscoverContext("api", new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        prompts.Should().HaveCount(2, "one discovery call per repository");
        prompts[0].Should().Contain("\"PrimaryLanguage\": \"csharp\"",
            "the api repo is described by a map published under a key it owns");
        prompts[0].Should().NotContain("\"PrimaryLanguage\": \"typescript\"");
        prompts[1].Should().Contain("\"PrimaryLanguage\": \"typescript\"",
            "the web repo is described by its own, not by the first entry in the dictionary");
    }

    [Fact]
    public async Task BootstrapRound_MultiGroupRepo_ResolvesItsOwnProjectMap()
    {
        // One repository with two toolchain groups, beside a second repository. The per-repo map
        // is the repository-level answer (ContextProjectMaps carries the per-context one, and a
        // checkpoint that predates it carries none), so what it must never be is another
        // repository's — which is what the dictionary's first entry is here.
        var captured = new CapturedPrompt();
        var pipeline = RoundPipeline();
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["other"] = NewMap("markdown"),
                ["app-api"] = NewMap(),
                ["app-web"] = NewMap(),
            });

        await NewRoundHandler(captured).ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "app", new AgentConfig(), pipeline,
                ContextName: "web", Workdir: "web"),
            CancellationToken.None);

        captured.User.Should().Contain("\"PrimaryLanguage\": \"csharp\"",
            "the round is handed a map published under a key its own repository owns");
        captured.User.Should().NotContain("\"PrimaryLanguage\": \"markdown\"",
            "the other repository's map is not this repository's, whatever order it sits in");
    }

    [Fact]
    public async Task BootstrapRound_RepoOwnsNoMap_StillFailsLoudly()
    {
        // The only published map belongs to another repository, and this run has ONE sandbox —
        // the shape whose count the round used to read as permission to take that map.
        var captured = new CapturedPrompt();
        var pipeline = RoundPipeline(sole: true);
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { ["other"] = NewMap("markdown") });

        var result = await NewRoundHandler(captured).ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "app", new AgentConfig(), pipeline,
                ContextName: "api", Workdir: "api"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("app").And.Contain("ProjectMap");
        captured.User.Should().BeEmpty(
            "a borrowed map describes another repository's tree — a failed init writes nothing");
    }

    private static PipelineContext DiscoverPipeline()
    {
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, Repos("api", "web"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api-core"] = Mock.Of<ISandbox>(),
                ["api-ui"] = Mock.Of<ISandbox>(),
                ["web-app"] = Mock.Of<ISandbox>(),
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["api-core"] = "api",
                ["api-ui"] = "api",
                ["web-app"] = "web",
            });
        // Keyed as AnalyzeProjectHandler publishes them: by the composed sandbox key.
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["api-core"] = NewMap(),
                ["api-ui"] = NewMap(),
                ["web-app"] = NewMap("typescript"),
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { DiscoverySkill });
        return pipeline;
    }

    /// <summary>
    /// One repository "app" with two toolchain groups, and a second repository "other".
    /// <paramref name="sole"/> narrows it to the single-sandbox run whose count the old
    /// fallback read as permission.
    /// </summary>
    private static PipelineContext RoundPipeline(bool sole = false)
    {
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, Repos("app", "other"));
        var api = new RemoteContextDiscovery("api", "api", "csharp");
        var web = new RemoteContextDiscovery("web", "web", "typescript");
        var docs = new RemoteContextDiscovery("docs", ".", "markdown");
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["app-api"] = Mock.Of<ISandbox>(),
        };
        var owners = new Dictionary<string, string>(StringComparer.Ordinal) { ["app-api"] = "app" };
        var contexts = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
        {
            ["app-api"] = [api],
        };
        if (!sole)
        {
            sandboxes["app-web"] = Mock.Of<ISandbox>();
            sandboxes["other"] = Mock.Of<ISandbox>();
            owners["app-web"] = "app";
            owners["other"] = "other";
            contexts["app-web"] = [web];
            contexts["other"] = [docs];
        }
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, owners);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts, contexts);
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        return pipeline;
    }

    private static PipelineContext NewPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static RepoConnection[] Repos(params string[] names) =>
        [.. names.Select(n => new RepoConnection { Name = n, Url = "https://x/y.git", Auth = "t" })];

    private static BootstrapDiscoverHandler NewDiscoverHandler(List<string> prompts) => new(
        new StubChatClientFactory(new PromptRecordingChatClient(prompts, DiscoveryAnswer)),
        null, EventTestStubs.RunContext, new DiscoveryOutputParser(), new SandboxTargets(),
        new AgenticToolSurface(), NullLogger<BootstrapDiscoverHandler>.Instance);

    private static BootstrapRoundHandler NewRoundHandler(CapturedPrompt captured) => new(
        new StubChatClientFactory(new CapturingChatClient(captured)),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(), new SandboxFileReaderFactory(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(),
            ContextGates.Build(), ContextGates.Writer(), ContextGates.DerivationStamp()),
        BootstrapReaderStubs.NullMetaFiles(),
        PrinciplesTransferStubs.NoTemplates(),
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        new SandboxTargets(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static ProjectMap NewMap(string primaryLanguage = "csharp") =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    private sealed class CapturedPrompt
    {
        public string User { get; set; } = string.Empty;
    }

    /// <summary>The rendered ProjectMap is in the user prompt, so the prompt is the evidence
    /// of which map the handler resolved.</summary>
    private sealed class PromptRecordingChatClient(List<string> prompts, string answer) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            prompts.Add(messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class CapturingChatClient(CapturedPrompt sink) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            sink.User = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StubChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
