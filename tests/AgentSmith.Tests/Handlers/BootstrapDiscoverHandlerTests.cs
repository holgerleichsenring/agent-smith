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
/// per RepoConnection, publishes its answer as DiscoveredComponents, and fails
/// loud when the LLM returns status=ambiguous (headless transport).
/// <para>
/// 2026-09-23-9bb2: a re-init runs that same round. What the repository already
/// declares is stated inside the prompt as prior art; it is no longer the answer.
/// </para>
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
    public async Task BootstrapDiscover_ReInitWithExistingContexts_RunsTheRound()
    {
        // 2026-09-23-9bb2: a repository that already carries .agentsmith/contexts/ used to
        // have its declaration returned as the answer, so a workdir derived wrongly once was
        // re-confirmed by every run of the command that produced it. The round runs, and the
        // components it publishes are the ones it derived.
        var response = """
            {
              "status": "complete",
              "components": [
                { "name": "server", "workdir": "src/server", "language": "csharp",     "evidence": "src/server/Program.cs" },
                { "name": "client", "workdir": "client",     "language": "typescript", "evidence": "client/package.json" }
              ]
            }
            """;
        var captured = new CapturedPrompt();
        var handler = NewHandler(response, captured);
        var pipeline = NewPipelineWithExistingDiscoveries(
            ("server", "server", "csharp"),
            ("client", "client", "typescript"));

        var result = await handler.ExecuteAsync(
            NewContext("monorepo", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Users.Should().ContainSingle("the model is called on a re-init exactly as on a first init");
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components["monorepo"].Should().HaveCount(2);
        components["monorepo"].Single(c => c.Name == "server").Workdir.Should().Be("src/server",
            "the derived workdir is the answer — the declared one was only the starting point");
    }

    [Fact]
    public async Task BootstrapDiscover_ReInitPrompt_StatesTheDeclaredComponentsAsPriorArt()
    {
        // The declaration reaches the round as what the LAST derivation concluded: each
        // context with its declared workdir and language, and a rule saying they are to be
        // verified against the tree rather than repeated back.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("server", "src/server", "csharp"), captured);
        var pipeline = NewPipelineWithExistingDiscoveries(
            ("server", "server", "csharp"),
            ("client", "client", "typescript"));

        await handler.ExecuteAsync(NewContext("monorepo", pipeline), CancellationToken.None);

        captured.User.Should().Contain("Previously derived",
            "the section names what it is before it lists anything");
        captured.User.Should().Contain("`server` — workdir `server`, language `csharp`");
        captured.User.Should().Contain("`client` — workdir `client`, language `typescript`");
        captured.User.Should().Contain("not ground truth",
            "a declaration the round is asked to confirm is a declaration that confirms itself");
    }

    [Fact]
    public async Task BootstrapDiscover_AColdInitPrompt_StatesNoPriorArt()
    {
        // A first init surfaces the synthetic default — name "default", workdir ".", no
        // language — which declares nothing. Stated as prior art it would be a root workdir
        // the round is invited to agree with.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("api", "src/api", "csharp"), captured);
        var pipeline = NewPipeline("api");
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["api"] = new("default", ".", null),
            });

        await handler.ExecuteAsync(NewContext("api", pipeline), CancellationToken.None);

        captured.User.Should().NotContain("Previously derived");
    }

    [Fact]
    public async Task BootstrapDiscover_ReInitAnswerCorrectsAWorkdir_CarriesTheNewOne()
    {
        // The live defect: meta.workdir was "." while the sources sit in a sub-directory, and
        // running init-project again returned "." because the declaration answered for the
        // round. The corrected workdir is what BootstrapDispatch is handed.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("default", "src/api", "csharp"), captured);
        var pipeline = NewPipelineWithExistingDiscoveries(("default", ".", "csharp"));

        var result = await handler.ExecuteAsync(
            NewContext("monorepo", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var components = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents);
        components["monorepo"].Single().Workdir.Should().Be("src/api",
            "the correction the round derived, not the declaration it started from");
        captured.User.Should().Contain("`default` — workdir `.`", "the wrong value is stated as prior art");
    }

    [Fact]
    public async Task ReInitPriorArt_MultiGroupRepo_ReachesTheRoundOfItsOwnRepo()
    {
        // p0322b regression (observed live on a 3-repo re-init): multi-group
        // repos get p0268 sandbox keys ("worker-csharp-500m-1gi", plus the "-2"
        // backstop) that the old BelongsToRepo string matcher — 'repo' and
        // 'repo/...' only — missed entirely. The repo projected an EMPTY
        // component list and BootstrapDispatchHandler fanned out ZERO rounds.
        // Ownership resolves via the coordinator's authoritative
        // ContextKeys.SandboxRepos map — 2026-09-23-9bb2: what it decides now is
        // which round is told about which declaration, and one repository's must
        // never be stated to another's.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("frontend", ".", "typescript"), captured);
        var pipeline = NewMultiGroupPipeline();

        var result = await handler.ExecuteAsync(
            NewContext("web", pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Users.Should().HaveCount(2, "one round per repo");
        captured.Users[0].Should().Contain("`frontend` — workdir `.`");
        captured.Users[0].Should().NotContain("`api`", "another repository's contexts are not this one's prior art");
        captured.Users[1].Should().Contain("`api` — workdir `src/api`");
        captured.Users[1].Should().Contain("`jobs` — workdir `src/jobs`",
            "both toolchain-group keys belong to the worker repo");
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

    [Fact]
    public async Task BootstrapDiscover_TheUserPrompt_TiesTheWorkdirToTheTreeNotTheStackCount()
    {
        // 2026-09-23-7868a: the round was told to answer "." whenever it found one
        // component, so a repository whose sources sit in a sub-tree declared the root
        // and the analyzer scoped to that workdir read the wrong tree. The system half
        // is the skill body, stubbed in this fixture; the user half is the factory's.
        var response = """
            {
              "status": "complete",
              "components": [
                { "name": "default", "workdir": "src/api", "language": "csharp", "evidence": "src/api/Program.cs" }
              ]
            }
            """;
        var captured = new CapturedPrompt();
        var handler = NewHandler(response, captured);
        var pipeline = NewPipeline("api");

        await handler.ExecuteAsync(NewContext("api", pipeline), CancellationToken.None);

        captured.User.Should().NotContainEquivalentOf("single-component",
            "how many components a repo holds decides nothing about where one of them sits");
        // 2026-09-24-c71a: a live run answered "<Component>/src" for an Angular component whose
        // package.json sits one level up — the truthful answer to "where the SOURCE occupies".
        // The question now names a file the answerer can point at.
        captured.User.Should().NotContain("SOURCE occupies",
            "that wording is what recorded a component one level too deep");
        captured.User.Should().NotContain("src/api",
            "an example pointing INTO a source directory teaches the same mistake");
        captured.User.Should().Contain("COMPONENT ROOT",
            "the field description says the value is the component's root");
        captured.User.Should().Contain("package.json",
            "and names a manifest, because a manifest can be pointed at where a judgement cannot");
        captured.User.Should().Contain("evidence",
            "with the fallback for a component that has no manifest at all");
    }

    [Fact]
    public async Task BootstrapDiscover_TheTask_ExcludesAnInternallyConsumedLibrary()
    {
        // 2026-09-23-3332: the criterion excluded a CONSUMED library, so a class library
        // the solution consumes internally — shipping to no registry — was never reached
        // by it and a context was written per layer project. The skill body, which is this
        // same prompt's system half, excludes internal shared libraries by name; the user
        // half now says it in the same words.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("api", "src/api", "csharp"), captured);
        var pipeline = NewPipeline("api");

        await handler.ExecuteAsync(NewContext("api", pipeline), CancellationToken.None);

        captured.User.Should().Contain("internal shared libraries",
            "the task excludes what the skill body excludes, in the skill's own words");
        captured.User.Should().Contain("consumes internally",
            "a library needs no registry to be excluded — being consumed at all is enough");
        captured.User.Should().Contain("named for the layer it holds",
            "the shape that was mistaken for a component is named so it is recognised");
    }

    [Fact]
    public async Task BootstrapDiscover_TheTask_BoundsTheComponentCountByDeployment()
    {
        // 2026-09-23-7868a removed the only sentence bounding the COUNT along with the
        // workdir rule it was written as. The anchor returns as a statement about what is
        // deployed — never about the workdir, which stays read off the tree.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("api", "src/api", "csharp"), captured);
        var pipeline = NewPipeline("api");

        await handler.ExecuteAsync(NewContext("api", pipeline), CancellationToken.None);

        captured.User.Should().Contain("deploys one thing has",
            "a reader can tell how many components a one-deployment repository has");
        captured.User.Should().Contain("however many projects",
            "the project count is named as what does NOT decide the component count");
        captured.User.Should().NotContainEquivalentOf("single-component",
            "the anchor bounds the count, and must not bring back the workdir rule with it");
    }

    [Fact]
    public async Task BootstrapDiscover_ThePriorArt_BindsAddingToTheCriterion()
    {
        // 2026-09-23-9bb2 closed the prior art with "add a component that was missed",
        // an invitation to add with nothing bounding what may be added. Adding stays
        // possible — a repository genuinely grows — but the criterion is what admits it.
        var captured = new CapturedPrompt();
        var handler = NewHandler(CompleteWith("server", "src/server", "csharp"), captured);
        var pipeline = NewPipelineWithExistingDiscoveries(("server", "server", "csharp"));

        await handler.ExecuteAsync(NewContext("monorepo", pipeline), CancellationToken.None);

        captured.User.Should().Contain("add a component the criterion below proves",
            "the permission to add survives, bound to the criterion instead of beside it");
        captured.User.Should().NotContain("add a component that was missed",
            "an unbounded invitation to add is what put a layer project in the answer");
    }

    private static string CompleteWith(string name, string workdir, string language) =>
        $$"""
          {
            "status": "complete",
            "components": [
              { "name": "{{name}}", "workdir": "{{workdir}}", "language": "{{language}}", "evidence": "{{workdir}}/entry" }
            ]
          }
          """;

    private static BootstrapDiscoverHandler NewHandler(
        string canned,
        CapturedPrompt? captured = null,
        IDialogueTransport? dialogueTransport = null)
    {
        var chat = new CannedChatClient(canned, captured);
        var factory = new CannedChatClientFactory(chat);
        return new BootstrapDiscoverHandler(
            factory, dialogueTransport, EventTestStubs.RunContext,
            new DiscoveryOutputParser(),
            new SandboxTargets(), new AgentSmith.Application.Services.Tools.AgenticToolSurface(), NullLogger<BootstrapDiscoverHandler>.Instance);
    }

    private static ProjectMap StubMap => new(
        PrimaryLanguage: "csharp",
        Frameworks: [],
        Modules: [],
        TestProjects: [],
        EntryPoints: [],
        Conventions: new Conventions(null, null, null),
        Ci: new CiConfig(false, null, null, null));

    private static BootstrapDiscoverContext NewContext(string repoName, PipelineContext pipeline)
        => new(repoName, new AgentConfig(), pipeline);

    private static PipelineContext NewPipeline(string repoName)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
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
                [repoName] = StubMap,
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { DiscoverySkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static PipelineContext NewMultiGroupPipeline()
    {
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
        // Each key the coordinator composed carries its own sandbox and its own analysis map;
        // the round resolves both through the same ownership test.
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["web"] = Mock.Of<ISandbox>(),
                ["worker-csharp-500m-1gi"] = Mock.Of<ISandbox>(),
                ["worker-csharp-500m-1gi-2"] = Mock.Of<ISandbox>(),
            });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["web"] = StubMap,
                ["worker-csharp-500m-1gi"] = StubMap,
                ["worker-csharp-500m-1gi-2"] = StubMap,
            });
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
        /// <summary>2026-09-23-9bb2: the user half of EVERY call, in order — a multi-repo run
        /// makes one per repo and each carries only that repository's prior art.</summary>
        public List<string> Users { get; } = [];
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
                captured.Users.Add(captured.User);
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
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, AgentSmith.Contracts.Providers.MasterLoopHooks? masterLoopHooks = null) => client;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
