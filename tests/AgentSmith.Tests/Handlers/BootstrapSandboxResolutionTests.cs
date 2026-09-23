using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
/// 2026-09-23-6698: which sandbox a bootstrap round reads and writes.
/// <para>
/// Both rounds looked a sandbox up by REPO NAME against a dictionary keyed by the
/// composed sandbox key. That key equals the repo name in one run shape of four — the
/// other three key on "default", on the context name, or on repo-plus-context — so the
/// lookup missed and each round took an arbitrary sandbox: discovery the first in the
/// dictionary, the round the singular back-compat slot. On one repository with two
/// toolchain groups every round ran in the first group's pod, writing a context.yaml
/// derived from another context's tree.
/// </para>
/// </summary>
public sealed class BootstrapSandboxResolutionTests
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
    public async Task BootstrapDiscover_MultiRepoMultiGroupKeys_ResolvesItsOwnRepoSandbox()
    {
        // Two repos, the first of them with two toolchain groups: NO key equals a repo
        // name, and the dictionary's first entry belongs to the other repository.
        var apiCore = new StubSandbox();
        var apiUi = new StubSandbox();
        var webApp = new StubSandbox();
        var pipeline = DiscoverPipeline(apiCore, apiUi, webApp);
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["api-core"] = "api",
                ["api-ui"] = "api",
                ["web-app"] = "web",
            });

        var result = await NewDiscoverHandler().ExecuteAsync(
            new BootstrapDiscoverContext("api", new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        webApp.RanSteps.Should().NotBeEmpty(
            "the web repo's discovery must read the sandbox its own repository owns");
        apiCore.RanSteps.Should().ContainSingle(
            "the api repo reads once, through a sandbox of its own — not once per repo in the dictionary");
        apiUi.RanSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task BootstrapDiscover_NoOwnerMap_StillResolvesByTheKeyScheme()
    {
        // No ContextKeys.SandboxRepos — a checkpoint that predates the map, or a fixture
        // that never seeds one. The ownership test decodes the key scheme instead.
        var apiCore = new StubSandbox();
        var apiUi = new StubSandbox();
        var webApp = new StubSandbox();
        var pipeline = DiscoverPipeline(apiCore, apiUi, webApp);

        var result = await NewDiscoverHandler().ExecuteAsync(
            new BootstrapDiscoverContext("api", new AgentConfig(), pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        webApp.RanSteps.Should().NotBeEmpty(
            "'web-app' decodes to the web repo without any map");
        apiCore.RanSteps.Should().ContainSingle();
        apiUi.RanSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task BootstrapRound_OneRepoTwoToolchainGroups_ResolvesItsOwnContextSandbox()
    {
        // One repository, two toolchain groups: the keys are the contexts' own names and
        // the repository owns BOTH sandboxes. The round is dispatched once per context,
        // so the context is what says which of the two is this round's.
        var apiSandbox = new StubSandbox();
        var webSandbox = new StubSandbox();
        var pipeline = RoundPipeline(apiSandbox, webSandbox);

        var result = await NewRoundHandler(Document("web"), contextName: "web").ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "app", new AgentConfig(), pipeline,
                ContextName: "web", Workdir: "web"),
            CancellationToken.None);

        webSandbox.RanSteps.Should().Contain(s => s.Kind == StepKind.WriteFile,
            "the round for context 'web' writes into the sandbox that holds it");
        apiSandbox.RanSteps.Should().BeEmpty(
            "the first group's pod is another context's tree, however it is addressed");
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task BootstrapRound_RepoOwnsNoSandbox_StillFailsNamingTheRepo()
    {
        var foreignSandbox = new StubSandbox();
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, Repos("app", "other"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["other"] = foreignSandbox });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["other"] = "other" });
        // The singular back-compat slot the round used to borrow from.
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, foreignSandbox);
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { ["app"] = NewMap() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { BootstrapSkill });

        var result = await NewRoundHandler(Document("api"), contextName: "api").ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "app", new AgentConfig(), pipeline,
                ContextName: "api", Workdir: "api"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("app").And.Contain("no sandbox");
        foreignSandbox.RanSteps.Should().BeEmpty(
            "a borrowed sandbox writes a context derived from another tree — a failed init writes nothing");
    }

    private static PipelineContext DiscoverPipeline(
        StubSandbox apiCore, StubSandbox apiUi, StubSandbox webApp)
    {
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, Repos("api", "web"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api-core"] = apiCore,
                ["api-ui"] = apiUi,
                ["web-app"] = webApp,
            });
        // 2026-09-23-bb73: keyed by SANDBOX KEY, the way AnalyzeProjectHandler publishes them.
        // Keyed by repository name this fixture certified a lookup production cannot make.
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

    private static PipelineContext RoundPipeline(StubSandbox apiSandbox, StubSandbox webSandbox)
    {
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, Repos("app"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api"] = apiSandbox,
                ["web"] = webSandbox,
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["api"] = "app",
                ["web"] = "app",
            });
        var api = new RemoteContextDiscovery("api", "api", "csharp");
        var web = new RemoteContextDiscovery("web", "web", "typescript");
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["api"] = api,
                ["web"] = web,
            });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["api"] = [api],
                ["web"] = [web],
            });
        // The first key the coordinator composed — the slot the round used to fall back to.
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, apiSandbox);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ProjectMap>>>(
            ContextKeys.ContextProjectMaps,
            new Dictionary<string, IReadOnlyDictionary<string, ProjectMap>>(StringComparer.Ordinal)
            {
                ["app"] = new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
                {
                    ["api"] = NewMap(),
                    ["web"] = NewMap("typescript"),
                },
            });
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

    private static BootstrapDiscoverHandler NewDiscoverHandler() => new(
        new StubChatClientFactory(new ReadingChatClient(DiscoveryAnswer)),
        null, EventTestStubs.RunContext, new DiscoveryOutputParser(), new SandboxTargets(),
        new AgenticToolSurface(), NullLogger<BootstrapDiscoverHandler>.Instance);

    private static BootstrapRoundHandler NewRoundHandler(string document, string contextName) => new(
        new StubChatClientFactory(new WritingChatClient(document, contextName)),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(), new SandboxFileReaderFactory(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(),
            ContextGates.Build(), ContextGates.Writer(), ContextGates.DerivationStamp()),
        // The real reader over the stub sandbox: what the round wrote is what the
        // round's own existence check then finds.
        BootstrapReaderStubs.MetaFilesOver(new SandboxFileReaderFactory()),
        PrinciplesTransferStubs.Composing("# Coding Principles\ncore + delta\n"),
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        new SandboxTargets(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static string Document(string workdir) =>
        $$"""{ "meta": { "workdir": "{{workdir}}" }, "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" } }""";

    private static ProjectMap NewMap(string primaryLanguage = "csharp") =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    /// <summary>Reads one file through the offered tools — which sandbox answered is the
    /// only evidence of which one the handler resolved.</summary>
    private sealed class ReadingChatClient(string answer) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var read = options?.Tools?.OfType<AIFunction>()
                .FirstOrDefault(function => function.Name == "read_file");
            if (read is not null)
                await read.InvokeAsync(
                    new AIFunctionArguments { ["path"] = "README.md" }, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, answer));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class WritingChatClient(string document, string contextName) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await BootstrapToolCall.WriteContextYamlAsync(options, document, contextName);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
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
