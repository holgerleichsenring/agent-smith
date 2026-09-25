using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-25-2fa7: the repository sweep asks for the role NAMED for it. codeMapGeneration was
/// declared, defaulted, mapped, rendered in the Config Studio and measured by preflight, and no
/// code ever requested it — an operator who set it changed nothing and was told nothing.
/// <para>
/// These drive the REAL <see cref="ChatClientFactory"/>. 2026-09-23-03b8 recorded why: a fake
/// resolves no model and grants no loop, so a role assertion through one stays green while the
/// function-invocation middleware is gone — which for THIS role would leave the analyzer holding
/// a tool list it can never call, and reading nothing.
/// </para>
/// </summary>
public sealed class CodeMapRoleBindingTests
{
    private const string PrimaryModel = "primary-model";
    private const string ScoutModel = "scout-model";

    [Fact]
    public void Create_TheCodeMapRole_ReturnsAFunctionInvokingClient()
    {
        var factory = RealFactory(new RecordingBuilder());

        factory.Create(Agent(codeMap: null), TaskType.CodeMapGeneration)
            .Should().BeOfType<FunctionInvokingChatClient>(
                "the sweep explores a repository WITH tools — a task outside ToolBearingTasks "
                + "returns before the middleware and the model could never call them");
    }

    [Fact]
    public void Create_AnAgentConfiguringNoCodeMapRole_ResolvesExactlyAsScoutDoesToday()
    {
        // p0374 put this sweep on the scout model after 450k+ tokens a run went through the
        // flagship one. Wiring the role must not quietly undo a measurement.
        var builder = new RecordingBuilder();
        var factory = RealFactory(builder);
        var agent = Agent(codeMap: null);

        factory.Create(agent, TaskType.CodeMapGeneration);

        builder.Models.Should().Equal([ScoutModel]);
        factory.GetMaxOutputTokens(agent, TaskType.CodeMapGeneration)
            .Should().Be(factory.GetMaxOutputTokens(agent, TaskType.Scout));
    }

    [Fact]
    public void Create_AnAgentConfiguringTheCodeMapRole_IsHonoured()
    {
        var builder = new RecordingBuilder();
        var factory = RealFactory(builder);
        var agent = Agent(codeMap: new ModelAssignment
        {
            Model = "code-map-model", MaxTokens = 6666, Deployment = "stub",
        });

        factory.Create(agent, TaskType.CodeMapGeneration);

        builder.Models.Should().Equal(["code-map-model"]);
        factory.GetMaxOutputTokens(agent, TaskType.CodeMapGeneration).Should().Be(6666);
    }

    [Fact]
    public void GetModel_ANonClaudeAgentWithNoCodeMapRole_IsNotHandedAClaudeModel()
    {
        // The registry entry used to be a hard-coded Claude model with no fallback. Inert while
        // nothing requested the role; the moment something does, an OpenAI or ollama agent would
        // ask its own provider for a model that provider has never heard of.
        var registry = new ConfigBasedModelRegistry(
            Agent(codeMap: null).Models!, NullLogger.Instance);

        registry.GetModel(TaskType.CodeMapGeneration).Model
            .Should().Be(ScoutModel).And.NotContain("claude");
    }

    private static AgentConfig Agent(ModelAssignment? codeMap) => new()
    {
        Type = "stub",
        Model = PrimaryModel,
        Models = new ModelRegistryConfig
        {
            Primary = new() { Model = PrimaryModel, MaxTokens = 7777, Deployment = "stub" },
            Scout = new() { Model = ScoutModel, MaxTokens = 1111, Deployment = "stub" },
            CodeMapGeneration = codeMap,
        },
    };

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

    private sealed class RecordingBuilder : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["stub"];
        public List<string> Models { get; } = [];

        public IChatClient Build(AgentConfig agent, ModelAssignment assignment)
        {
            Models.Add(assignment.Model);
            return new SilentChat();
        }
    }

    private sealed class SilentChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

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
