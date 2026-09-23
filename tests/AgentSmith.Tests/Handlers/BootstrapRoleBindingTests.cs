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
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-23-03b8: the two bootstrap rounds call the context-generation role, so the model
/// an operator assigns to it is the model that writes their contexts — and they still get the
/// tool loop, because a task outside <see cref="ChatClientFactory.ToolBearingTasks"/> returns
/// before the function-invocation middleware and would leave both rounds reading nothing.
/// <para>
/// The last two tests drive the REAL <see cref="ChatClientFactory"/>. A fake factory resolves
/// no model and grants no loop, so neither half of the claim is observable through one.
/// </para>
/// </summary>
public sealed class BootstrapRoleBindingTests
{
    private const string PrimaryModel = "primary-model";
    private const int PrimaryCeiling = 7777;
    private const string StatedModel = "stated-context-model";
    private const int StatedCeiling = 4242;

    [Fact]
    public async Task Bootstrap_BothRounds_AskForTheContextGenerationRole()
    {
        var factory = new TaskRecordingFactory();

        await RunDiscoverAsync(factory, Agent(contextGeneration: null));
        await RunRoundAsync(factory, Agent(contextGeneration: null));

        factory.ClientTasks.Should().Equal(
            [TaskType.ContextGeneration, TaskType.ContextGeneration],
            "both rounds pick the model for the role an operator assigns to context generation");
        factory.CeilingTasks.Should().Equal(
            [TaskType.ContextGeneration, TaskType.ContextGeneration],
            "the output ceiling has to come from the same role, or the round writes against "
            + "a budget belonging to a model it is not calling");
    }

    [Fact]
    public async Task Bootstrap_RoleUnset_CallsTheSameModelAsPrimary()
    {
        // The role is optional (2026-09-23-7868b) and resolves to primary when unset, so an
        // installation that stated nothing must call exactly what it called before binding —
        // the same model AND the same output ceiling.
        var builder = new AssignmentRecordingBuilder();
        var factory = RealFactory(builder);
        var agent = Agent(contextGeneration: null);

        await RunDiscoverAsync(factory, agent);
        await RunRoundAsync(factory, agent);

        builder.Models.Should().Equal([PrimaryModel, PrimaryModel]);
        builder.Chat.Ceilings.Should().Equal([PrimaryCeiling, PrimaryCeiling]);
    }

    [Fact]
    public async Task Bootstrap_RoleAssigned_CallsThatModelWithItsTools()
    {
        var builder = new AssignmentRecordingBuilder();
        var factory = RealFactory(builder);
        var agent = Agent(contextGeneration: new ModelAssignment
        {
            Model = StatedModel, MaxTokens = StatedCeiling, Deployment = "stub",
        });

        await RunDiscoverAsync(factory, agent);
        await RunRoundAsync(factory, agent);

        builder.Models.Should().Equal([StatedModel, StatedModel]);
        builder.Chat.Ceilings.Should().Equal([StatedCeiling, StatedCeiling]);
        builder.Chat.SawTools.Should().Equal([true, true], "both rounds hand over a tool list");
        factory.Create(agent, TaskType.ContextGeneration)
            .Should().BeOfType<FunctionInvokingChatClient>(
                "a task outside ToolBearingTasks returns before the function-invocation "
                + "middleware, so the tools both rounds hand over would never be executed");
    }

    // ---- the two rounds, run over whichever factory a test supplies ----

    private static Task<CommandResult> RunDiscoverAsync(IChatClientFactory factory, AgentConfig agent)
    {
        var handler = new BootstrapDiscoverHandler(
            factory, dialogueTransport: null, EventTestStubs.RunContext,
            new DiscoveryOutputParser(), new SandboxTargets(), new AgenticToolSurface(),
            NullLogger<BootstrapDiscoverHandler>.Instance);
        return handler.ExecuteAsync(
            new BootstrapDiscoverContext(RepoName, agent, DiscoverPipeline()), CancellationToken.None);
    }

    private static Task<CommandResult> RunRoundAsync(IChatClientFactory factory, AgentConfig agent)
    {
        var handler = new BootstrapRoundHandler(
            factory,
            new BootstrapToolHostFactory(
                Mock.Of<IDecisionLogger>(), new SandboxFileReaderFactory(),
                new PathReadGuard(new NullGitIgnoreResolver()),
                new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
                ContextGates.Serializer(), ContextGates.Build(), ContextGates.Writer(),
                ContextGates.DerivationStamp()),
            BootstrapReaderStubs.NullMetaFiles(),
            PrinciplesTransferStubs.NoTemplates(),
            new BootstrapContextWriteVerdict(),
            new BootstrapOutputRecorder(),
            new SandboxTargets(),
            EventTestStubs.RunContext,
            NullLogger<BootstrapRoundHandler>.Instance);
        return handler.ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, RepoName, agent, RoundPipeline(),
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);
    }

    // ---- fixtures ----

    private const string RepoName = "api";

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

    /// <summary>Every other role carries a model of its own, so a round that asked for the
    /// wrong one names it in the failure instead of resolving to the same string.</summary>
    private static AgentConfig Agent(ModelAssignment? contextGeneration) => new()
    {
        Type = "stub",
        Model = PrimaryModel,
        Models = new ModelRegistryConfig
        {
            Primary = new() { Model = PrimaryModel, MaxTokens = PrimaryCeiling, Deployment = "stub" },
            Scout = new() { Model = "scout-model", MaxTokens = 1111, Deployment = "stub" },
            Planning = new() { Model = "planning-model", MaxTokens = 2222, Deployment = "stub" },
            Reasoning = new() { Model = "reasoning-model", MaxTokens = 3333, Deployment = "stub" },
            Summarization = new() { Model = "summarization-model", MaxTokens = 5555, Deployment = "stub" },
            CodeMapGeneration = new() { Model = "code-map-model", MaxTokens = 6666, Deployment = "stub" },
            ContextGeneration = contextGeneration,
        },
    };

    private static PipelineContext BasePipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [RepoName] = Mock.Of<ISandbox>() });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [RepoName] = Map() });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static PipelineContext DiscoverPipeline()
    {
        var pipeline = BasePipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos,
            new[] { new RepoConnection { Name = RepoName, Url = "https://x/y.git", Auth = "test" } });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { DiscoverySkill });
        return pipeline;
    }

    private static PipelineContext RoundPipeline()
    {
        var pipeline = BasePipeline();
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        return pipeline;
    }

    private static ProjectMap Map() =>
        new(PrimaryLanguage: "csharp",
            Frameworks: [], Modules: [], TestProjects: [], EntryPoints: [],
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(false, null, null, null));

    /// <summary>A discovery answer the parser accepts, so the round reaches its own end.</summary>
    private const string DiscoveryAnswer = """
        {
          "status": "complete",
          "components": [
            { "name": "default", "workdir": ".", "language": "csharp", "evidence": "src/Api/Program.cs" }
          ]
        }
        """;

    private static ChatClientFactory RealFactory(IChatClientBuilder builder) =>
        new(
            [builder],
            EventTestStubs.NoOp,
            EventTestStubs.RunContext,
            new ModelPricingResolver(),
            new UnlimitedRateLimiters(),
            new ThrottleWaitReporter(),
            new NullRunTraceWriter(),
            TurnActivityRecorder.Silent(),
            new CompactionSummaryRequest(),
            new WindowDerivedCompaction(),
            NullLoggerFactory.Instance);

    // ---- doubles ----

    /// <summary>Records which task each round named, for the client and for the ceiling.</summary>
    private sealed class TaskRecordingFactory : IChatClientFactory
    {
        public List<TaskType> ClientTasks { get; } = [];
        public List<TaskType> CeilingTasks { get; } = [];

        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null)
        {
            ClientTasks.Add(task);
            return new CannedChat(null);
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task)
        {
            CeilingTasks.Add(task);
            return 8192;
        }

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    /// <summary>Records the assignment the real factory resolved for each round.</summary>
    private sealed class AssignmentRecordingBuilder : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["stub"];
        public List<string> Models { get; } = [];
        public CannedChat Chat { get; } = new(DiscoveryAnswer);

        public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
        {
            Models.Add(assignment.Model);
            return Chat;
        }
    }

    /// <summary>Answers every call the same way and records the options it was handed.</summary>
    private sealed class CannedChat(string? canned) : IChatClient
    {
        public List<int?> Ceilings { get; } = [];
        public List<bool> SawTools { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Ceilings.Add(options?.MaxOutputTokens);
            SawTools.Add(options?.Tools is { Count: > 0 });
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, canned ?? "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>No budget to queue against — these tests build clients and call them once.</summary>
    private sealed class UnlimitedRateLimiters : ILlmRateLimiterRegistry, ILlmRateLimiter
    {
        public ILlmRateLimiter GetOrCreate(
            string providerType, string model, LlmRateLimitOptions options) => this;

        public Task<IDisposable> AcquireAsync(
            int estimatedInputTokens, CancellationToken cancellationToken) =>
            Task.FromResult<IDisposable>(new Lease());

        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
}
