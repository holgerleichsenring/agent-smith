using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.PipelineHarness.Evals;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// 2026-09-23-e848: every factory that decides which tasks get a tool loop grants it for the
/// same tasks — the list <see cref="ChatClientFactory.ToolBearingTasks"/> states.
/// <para>
/// Why it is worth a test: a factory that grants the loop for a different set drives the task
/// differently from production, so a recorded run and a scripted run can pass while the thing
/// they stand in for would not. Two of the four had already lost Reasoning that way.
/// </para>
/// <para>
/// It lives in the harness because this is the only assembly that can see all four: the
/// harness references AgentSmith.Tests, not the other way round.
/// </para>
/// </summary>
public sealed class ToolBearingTaskAgreementTests
{
    [Fact]
    public void ToolBearingTasks_EveryFactory_GrantsTheLoopForTheSameTasks()
    {
        var factories = Factories();
        var theList = Render(Enum.GetValues<TaskType>().Where(ChatClientFactory.ToolBearingTasks.Contains));

        var granted = factories
            .Select(f => $"{f.Name}: {Render(TasksGrantedTheLoopBy(f.Factory))}")
            .ToList();

        granted.Should().Equal(
            factories.Select(f => $"{f.Name}: {theList}"),
            "one list states which tasks take tools; a factory that grants the loop for a "
            + "different set is driving that task differently from production");
    }

    /// <summary>
    /// The set the scripted adapter had lost: a reasoning call takes tools in production, so a
    /// preset that scripts one has to execute them or it is proving a different component.
    /// </summary>
    [Fact]
    public void ScriptedFactory_ReasoningTask_GrantsTheToolLoop()
    {
        var factory = ScriptedChatClientFactoryAdapter.Untraced(new ScriptedChatClient());

        factory.Create(Agent(), TaskType.Reasoning)
            .Should().BeOfType<FunctionInvokingChatClient>(
                "production grants Reasoning a tool loop, so a scripted run of the same task "
                + "has to invoke the tools it is handed");
    }

    private static (string Name, IChatClientFactory Factory)[] Factories() =>
    [
        ("production", Production()),
        ("replay", new ReplayChatClientFactory(new ScriptedChatClient())),
        ("harness/scripted", ScriptedChatClientFactoryAdapter.Untraced(new ScriptedChatClient())),
        ("harness/eval", new AccountEvalChatFactory(new ScriptedChatClient(), "eval-model")),
    ];

    /// <summary>
    /// The loop is visible in the SHAPE of what a factory hands back: only the tool-bearing
    /// branch builds a <see cref="FunctionInvokingChatClient"/>, in all four.
    /// </summary>
    private static IEnumerable<TaskType> TasksGrantedTheLoopBy(IChatClientFactory factory) =>
        Enum.GetValues<TaskType>()
            .Where(task => factory.Create(Agent(), task) is FunctionInvokingChatClient);

    private static string Render(IEnumerable<TaskType> tasks) =>
        string.Join(", ", tasks.Select(t => t.ToString()).OrderBy(t => t, StringComparer.Ordinal));

    private static AgentConfig Agent() => new()
    {
        Type = "stub",
        Model = "m",
        Models = new ModelRegistryConfig(),
    };

    private static ChatClientFactory Production() =>
        new(
            [new StubBuilder()],
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

    private sealed class StubBuilder : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["stub"];

        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) =>
            new ScriptedChatClient();
    }

    /// <summary>No budget to queue against — this test builds clients, it never calls one.</summary>
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
