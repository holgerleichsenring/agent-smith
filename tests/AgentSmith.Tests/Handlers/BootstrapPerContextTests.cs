using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0161d: per-context bootstrap-round behaviour — paths flow through
/// MetaDirFor, applies_to renders in execution prompts, RoundContext
/// carries ContextName + Workdir, and the prompt factory emits per-context
/// write targets.
/// </summary>
public sealed class BootstrapPerContextTests
{
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

    private static ProjectMap NewMap(string primaryLanguage = "csharp") =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    [Fact]
    public void BootstrapRoundContext_CarriesContextNameAndWorkdir()
    {
        // Smoke for the type-shape change. The handler reads these to scope
        // its tool-host and prompt; downstream test fixtures rely on the
        // record's defaults to migrate without rewriting every call site.
        var ctx = new BootstrapRoundContext(
            SkillName: "project-bootstrap",
            RepoName: "monorepo",
            AgentConfig: new AgentConfig(),
            Pipeline: new PipelineContext(),
            ContextName: "server",
            Workdir: "server");

        ctx.ContextName.Should().Be("server");
        ctx.Workdir.Should().Be("server");
        ctx.RepoName.Should().Be("monorepo");
    }

    [Fact]
    public async Task BootstrapPromptFactory_PathsUsePerContextMetaDir()
    {
        // p0161d: BootstrapRoundHandler must hand BootstrapPromptFactory the
        // ContextName from the round; the user prompt must name the per-context
        // write targets and the workdir, never the flat .agentsmith/context.yaml.
        var captured = new CapturedPrompt();
        var handler = new BootstrapRoundHandler(
            new PromptCapturingFactory(new CapturingChatClient(captured)),
            new BootstrapToolHostFactory(Mock.Of<IDecisionLogger>()),
            NullLogger<BootstrapRoundHandler>.Instance);
        var pipeline = NewSingleSandboxPipeline("monorepo");

        await handler.ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        captured.User.Should().Contain($"{ProjectMetaPaths.Contexts}/server/{ProjectMetaPaths.ContextYamlFile}");
        captured.User.Should().Contain($"{ProjectMetaPaths.Contexts}/server/{ProjectMetaPaths.CodingPrinciplesFile}");
        captured.User.Should().Contain("Workdir (repo-relative): server");
        // Negative: flat root path must NOT appear when context is set.
        captured.User.Should().NotContain($".agentsmith/{ProjectMetaPaths.ContextYamlFile}\n");
    }

    [Fact]
    public async Task BootstrapPromptFactory_AppliesToFlowsIntoBootstrapPrompt()
    {
        // p0161d step 8: PhaseAppliesTo renders an "Applies to: ..." line in the
        // bootstrap user prompt when set.
        var captured = new CapturedPrompt();
        var handler = new BootstrapRoundHandler(
            new PromptCapturingFactory(new CapturingChatClient(captured)),
            new BootstrapToolHostFactory(Mock.Of<IDecisionLogger>()),
            NullLogger<BootstrapRoundHandler>.Instance);
        var pipeline = NewSingleSandboxPipeline("monorepo");
        pipeline.Set(ContextKeys.PhaseAppliesTo, "Application (BootstrapDispatch)");

        await handler.ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        captured.User.Should().Contain("Applies to: Application (BootstrapDispatch)");
    }

    [Fact]
    public void AppliesToFlowsIntoExecutionPrompt_WhenSet()
    {
        // p0161d step 8: AgentPromptBuilder.BuildExecutionUserPrompt renders an
        // "Applies to: <value>" line near the top when the optional appliesTo
        // argument is non-empty; empty/null emits nothing (back-compat).
        var prompts = new Mock<IPromptCatalog>();
        var builder = new AgentPromptBuilder(prompts.Object);
        var plan = new Plan("test", new List<PlanStep> { new(1, "noop", new FilePath("x"), "Create") }, "{}");
        var repo = new Repository(new BranchName("main"), "https://x/y.git");

        var withApplies = builder.BuildExecutionUserPrompt(
            plan, repo, appliesTo: "Application (BootstrapDispatchHandler)");
        var withoutApplies = builder.BuildExecutionUserPrompt(plan, repo);

        withApplies.Should().Contain("Applies to: Application (BootstrapDispatchHandler)");
        withoutApplies.Should().NotContain("Applies to:");
    }

    private static PipelineContext NewSingleSandboxPipeline(string repoName)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills/coding", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [repoName] = Mock.Of<ISandbox>() });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [repoName] = NewMap() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private sealed class CapturedPrompt
    {
        public string System { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }

    private sealed class CapturingChatClient(CapturedPrompt sink) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            sink.System = list.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
            sink.User = list.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class PromptCapturingFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
