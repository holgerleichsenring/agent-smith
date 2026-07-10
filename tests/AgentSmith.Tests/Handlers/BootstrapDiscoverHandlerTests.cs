using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0161d: BootstrapDiscoverHandler runs the read-only discovery skill once
/// per RepoConnection on cold-init, projects existing contexts/ into
/// DiscoveredComponents on re-init, and fails loud when the LLM returns
/// status=ambiguous (headless transport).
/// </summary>
public sealed class BootstrapDiscoverHandlerTests
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
        ActivatesWhen = "pipeline_name = \"init-project\"",
    };

    [Fact]
    public async Task BootstrapDiscover_SingleComponentRepo_ReturnsOneEntry()
    {
        var response = """
            {
              "status": "complete",
              "components": [
                { "name": "default", "workdir": ".", "language": "csharp", "evidence": "src/Sample.Cli/Program.cs" }
              ]
            }
            """;
        var handler = NewHandler(response);
        var pipeline = NewPipeline("api");

        var result = await handler.ExecuteAsync(
            NewContext("api", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components.Should().ContainKey("api");
        components["api"].Should().HaveCount(1);
        components["api"][0].Name.Should().Be("default");
        components["api"][0].Language.Should().Be("csharp");
    }

    [Fact]
    public async Task BootstrapDiscover_ColdInitMultiComponentMonorepo_ListsAllComponents()
    {
        var response = """
            {
              "status": "complete",
              "components": [
                { "name": "server", "workdir": "server", "language": "csharp",     "evidence": "server/src/Sample.Api/Program.cs" },
                { "name": "client", "workdir": "client", "language": "typescript", "evidence": "client/package.json" },
                { "name": "docs",   "workdir": "docs",   "language": "markdown",   "evidence": "docs/index.md" }
              ]
            }
            """;
        var handler = NewHandler(response);
        var pipeline = NewPipeline("monorepo");

        var result = await handler.ExecuteAsync(
            NewContext("monorepo", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components["monorepo"].Should().HaveCount(3);
        components["monorepo"].Select(c => c.Name).Should().BeEquivalentTo(["server", "client", "docs"]);
        components["monorepo"].Single(c => c.Name == "client").Workdir.Should().Be("client");
        components["monorepo"].Single(c => c.Name == "docs").Language.Should().Be("markdown");
    }

    [Fact]
    public async Task BootstrapDiscover_AmbiguousComponents_FailsLoudInHeadlessMode()
    {
        // Headless transport: dialogueTransport is null in NewHandler, so the
        // prompt directs the LLM to return status=ambiguous instead of calling
        // ask_human. The handler must fail loud with the structured message
        // and populate ContextKeys.DiscoveryAmbiguous.
        var response = """
            {
              "status": "ambiguous",
              "components": [],
              "ambiguity": {
                "message": "Two roots both look deployable: top-level Dockerfile and server/Dockerfile",
                "candidates": ["root", "server"]
              }
            }
            """;
        var handler = NewHandler(response);
        var pipeline = NewPipeline("monorepo");

        var result = await handler.ExecuteAsync(
            NewContext("monorepo", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ambiguous");
        result.Message.Should().Contain("root");
        result.Message.Should().Contain("server");
        result.Message.Should().Contain("Re-run init-project via the CLI");
        pipeline.TryGet<string>(ContextKeys.DiscoveryAmbiguous, out var ambiguous).Should().BeTrue();
        ambiguous.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BootstrapDiscover_ReInitWithExistingContexts_SkipsDiscovery()
    {
        // Re-init path: SandboxDiscoveries already surfaces a non-synthetic
        // context (real .agentsmith/contexts/<name>/ on remote). The handler
        // short-circuits without calling the LLM and projects existing
        // discoveries into DiscoveredComponents.
        var handler = NewHandler(canned: "should not be called");
        var pipeline = NewPipelineWithExistingDiscoveries(
            ("server", "server", "csharp"),
            ("client", "client", "typescript"));

        var result = await handler.ExecuteAsync(
            NewContext("monorepo", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("re-init");
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components["monorepo"].Should().HaveCount(2);
        components["monorepo"].Select(c => c.Name).Should().Contain(["server", "client"]);
    }

    [Fact]
    public async Task ReInitProjection_MultiGroupRepo_KeepsItsComponents()
    {
        // p0322b regression (observed live on a 3-repo re-init): multi-group
        // repos get p0268 sandbox keys ("worker-csharp-500m-1gi", plus the "-2"
        // backstop) that the old BelongsToRepo string matcher — 'repo' and
        // 'repo/...' only — missed entirely. The repo projected an EMPTY
        // component list and BootstrapDispatchHandler fanned out ZERO rounds.
        // The projection must resolve ownership via the coordinator's
        // authoritative ContextKeys.SandboxRepos map.
        var handler = NewHandler(canned: "should not be called");
        var pipeline = NewPipeline("web");
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[]
        {
            new RepoConnection { Name = "web", Url = "https://x/web.git", Auth = "test" },
            new RepoConnection { Name = "worker", Url = "https://x/worker.git", Auth = "test" },
        });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["web"] = new("frontend", ".", "typescript"),
                ["worker-csharp-500m-1gi"] = new("api", "src/api", "csharp"),
                ["worker-csharp-500m-1gi-2"] = new("jobs", "src/jobs", "csharp"),
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["web"] = "web",
                ["worker-csharp-500m-1gi"] = "worker",
                ["worker-csharp-500m-1gi-2"] = "worker",
            });

        var result = await handler.ExecuteAsync(
            NewContext("web", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components["web"].Select(c => c.Name).Should().BeEquivalentTo(["frontend"]);
        components["worker"].Select(c => c.Name).Should().BeEquivalentTo(["api", "jobs"]);
    }

    [Fact]
    public async Task BootstrapDiscover_AmbiguousComponents_AsksHumanInInteractiveMode()
    {
        // Interactive transport: the prompt directs the LLM to call ask_human
        // instead of returning ambiguous. We can't simulate the full tool-call
        // loop in this unit test, but we can assert the prompt embedded the
        // ask_human guidance — the schema-validated headless guard works above.
        var response = """
            {
              "status": "complete",
              "components": [
                { "name": "default", "workdir": ".", "language": "csharp", "evidence": "src/Sample.Cli/Program.cs" }
              ]
            }
            """;
        var captured = new CapturedPrompt();
        var handler = NewHandler(response, captured, dialogueTransport: Mock.Of<IDialogueTransport>());
        var pipeline = NewPipeline("api");

        await handler.ExecuteAsync(NewContext("api", pipeline), CancellationToken.None);

        // Interactive guidance: explicit call to ask_human; headless "return
        // status=ambiguous from the tree alone" guidance must NOT be present.
        captured.User.Should().Contain("call `ask_human` once");
        captured.User.Should().NotContain("DO NOT guess");
    }

    private static BootstrapDiscoverHandler NewHandler(
        string canned,
        CapturedPrompt? captured = null,
        IDialogueTransport? dialogueTransport = null)
    {
        var chat = new CannedChatClient(canned, captured);
        var factory = new CannedChatClientFactory(chat);
        return new BootstrapDiscoverHandler(
            factory, dialogueTransport, EventTestStubs.RunContext,
            NullLogger<BootstrapDiscoverHandler>.Instance);
    }

    private static BootstrapDiscoverContext NewContext(string repoName, PipelineContext pipeline)
        => new(repoName, new AgentConfig(), pipeline);

    private static PipelineContext NewPipeline(string repoName)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, new[]
            {
                new RepoConnection { Name = repoName, Url = "https://x/y.git", Auth = "test" },
            });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [repoName] = Mock.Of<ISandbox>() });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                [repoName] = new ProjectMap(
                    PrimaryLanguage: "csharp",
                    Frameworks: [],
                    Modules: [],
                    TestProjects: [],
                    EntryPoints: [],
                    Conventions: new Conventions(null, null, null),
                    Ci: new CiConfig(false, null, null, null)),
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { DiscoverySkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static PipelineContext NewPipelineWithExistingDiscoveries(
        params (string ContextName, string Workdir, string Language)[] discoveries)
    {
        const string repoName = "monorepo";
        var pipeline = NewPipeline(repoName);
        var dict = new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal);
        foreach (var d in discoveries)
            dict[d.ContextName] = new RemoteContextDiscovery(d.ContextName, d.Workdir, d.Language);
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, dict);
        return pipeline;
    }

    public sealed class CapturedPrompt
    {
        public string System { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }

    private sealed class CannedChatClient(string canned, CapturedPrompt? captured) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (captured is not null)
            {
                var list = messages.ToList();
                captured.System = list.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
                captured.User = list.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, canned)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class CannedChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
