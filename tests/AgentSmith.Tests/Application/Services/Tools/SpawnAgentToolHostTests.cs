using System.Reflection;
using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SubAgents;

public sealed class SpawnAgentToolHostTests
{
    [Fact]
    public async Task SpawnAgentToolHost_GenericName_TaskFailsWithoutLlmCall()
    {
        var loopRunner = new CountingLoopRunner();
        var (sut, _) = BuildHost(loopRunner);

        var result = await Invoke(sut, """
        [{"name":"agent1","activity":"a","task_description":"t",
          "inherited_context":{"pipeline_goal":"g","prior_context_slice":"s"}}]
        """);

        result.Should().Contain("invalid_name");
        loopRunner.CallCount.Should().Be(0, "the generic name short-circuits before any LLM cost");
    }

    [Fact]
    public async Task SpawnAgentToolHost_OverBudget_TasksFailWithoutLlmCall()
    {
        var loopRunner = new CountingLoopRunner();
        var (sut, _) = BuildHost(loopRunner, budget: 1);

        var result = await Invoke(sut, """
        [
          {"name":"FirstScout","activity":"a","task_description":"t",
           "inherited_context":{"pipeline_goal":"g","prior_context_slice":"s"}},
          {"name":"SecondScout","activity":"a","task_description":"t",
           "inherited_context":{"pipeline_goal":"g","prior_context_slice":"s"}}
        ]
        """);

        result.Should().Contain("budget_exhausted");
        loopRunner.CallCount.Should().Be(1, "only the first task fits the budget");
    }

    [Fact]
    public async Task SpawnAgentToolHost_LogsDecision_CountNamesActivities()
    {
        var loopRunner = new CountingLoopRunner();
        var (sut, decisionLogger) = BuildHost(loopRunner);

        await Invoke(sut, """
        [{"name":"ContextMapInvestigator","activity":"map the repo","task_description":"t",
          "inherited_context":{"pipeline_goal":"g","prior_context_slice":"s"}}]
        """);

        decisionLogger.Decisions.Should().ContainSingle();
        decisionLogger.Decisions[0].Decision.Should().Contain("ContextMapInvestigator");
        decisionLogger.Decisions[0].Decision.Should().Contain("map the repo");
        decisionLogger.Decisions[0].Decision.Should().Contain("Spawned 1");
    }

    [Fact]
    public void SpawnAgentToolHost_ResultHasNoResultTextField()
    {
        var props = typeof(SubAgentResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        props.Should().NotContain(p => string.Equals(p.Name, "ResultText", StringComparison.OrdinalIgnoreCase));
    }

    private static (SpawnAgentToolHost host, RecordingDecisionLogger logger) BuildHost(
        IAgenticLoopRunner loopRunner, int budget = 100)
    {
        var publisher = new RecordingEventPublisher();
        var limits = new LoopLimitsConfig { MaxConcurrentSubAgents = 4 };
        var runner = new SubAgentRunner(loopRunner, publisher, limits, NullLogger<SubAgentRunner>.Instance);
        var pipeline = new PipelineContext();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        var policy = new AllHostsActivePolicy();
        var context = new SubAgentContext(
            pipeline, new Dictionary<string, ISandbox>(), tracker,
            MasterRunId: "run-1", new ToolKit(policy), policy,
            new SubAgentBudget(budget));
        var logger = new RecordingDecisionLogger();
        return (
            new SpawnAgentToolHost(
                runner, context.Budget, new SubAgentNameValidator(), logger, context),
            logger);
    }

    private static async Task<string> Invoke(SpawnAgentToolHost host, string tasksJson)
    {
        var tool = host.GetTools(phase: null, investigatorMode: null).Single();
        var parsed = JsonDocument.Parse(tasksJson);
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["tasks"] = parsed.RootElement,
        });
        return result?.ToString() ?? "";
    }

    private sealed class CountingLoopRunner : IAgenticLoopRunner
    {
        private int _count;
        public int CallCount => _count;
        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2 },
                },
                TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class RecordingDecisionLogger : IDecisionLogger
    {
        public List<(string? Repo, DecisionCategory Cat, string Decision)> Decisions { get; } = new();
        public Task LogAsync(string? repoPath, DecisionCategory category, string decision,
                             CancellationToken cancellationToken = default,
                             string? sourceLabel = null)
        {
            Decisions.Add((repoPath, category, decision));
            return Task.CompletedTask;
        }
    }
}
