using System.Text.Json;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.SubAgents;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Sanity;

public sealed class EmptySubAgentAnswerRuleTests
{
    private const string OneTask = "[{\"name\":\"RepoScout\",\"activity\":\"read the sources\","
        + "\"task_description\":\"answer one question\",\"inherited_context\":"
        + "{\"pipeline_goal\":\"build it\",\"prior_context_slice\":\"none\"}}]";

    [Fact]
    public async Task SpawnAgents_EmptyChild_RowCarriesEmptyAndAFrameworkLine()
    {
        var decisions = new RecordingDecisionLogger();
        var context = SubAgentRunnerTests.BuildContext();
        var host = new SpawnAgentToolHost(
            SubAgentRunnerTests.BuildRunner(new StubLoopRunner(answer: " ")), new SubAgentBudget(5),
            new SubAgentNameValidator(), decisions, context);
        var spawn = (AIFunction)TestSanity.Wrapper().Wrap(host.GetTools(null, null), context.Pipeline).Single();

        var result = (string)(await spawn.InvokeAsync(
            new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, CancellationToken.None))!;

        var lines = result.Split('\n');
        JsonDocument.Parse(lines[0]).RootElement.GetProperty("results")[0].GetProperty("status").GetString().Should().Be("Empty");
        lines[1].Should().StartWith("[framework] RepoScout (sa-").And.Contain("answered nothing");
        decisions.Lines.Single().Should().Contain("RepoScout (read the sources) — Empty");
    }

    [Fact]
    public async Task ReadSubAgentObservations_BlankAnswer_GetsAFrameworkLine()
    {
        var text = await Read(store => store.Store("sa-1", "   "), "sa-1");

        text.Should().Contain("\n[framework] sub-agent sa-1 answered nothing");
    }

    [Fact]
    public async Task ReadSubAgentObservations_UnknownIdNullAnswer_IsNotFlagged()
    {
        var text = await Read(_ => { }, "sa-unknown");

        text.Should().NotContain("[framework]").And.Contain("no answer recorded for this sub-agent");
    }

    [Fact]
    public async Task ReadSubAgentObservations_Answer_IsNotFlagged()
    {
        var text = await Read(store => store.Store("sa-1", "two endpoints"), "sa-1");

        text.Should().NotContain("[framework]");
    }

    private static async Task<string> Read(Action<IChildAnswerStore> seed, string id)
    {
        var store = new InMemoryChildAnswerStore();
        seed(store);
        var tool = (AIFunction)TestSanity.Wrapper()
            .Wrap(new ReadSubAgentObservationsToolHost(store).GetTools(null, null), SanityCalls.Pipeline()).Single();
        return ToolResultBound.TextOf(await tool.InvokeAsync(new AIFunctionArguments { ["sub_agent_id"] = id }, CancellationToken.None))!;
    }

    private sealed class RecordingDecisionLogger : IDecisionLogger
    {
        public List<string> Lines { get; } = [];

        public Task<DecisionLogOutcome> LogAsync(
            ISandboxFileReader? repositoryFiles, DecisionCategory category, string decision,
            CancellationToken cancellationToken = default, string? sourceLabel = null)
        {
            Lines.Add(decision);
            return Task.FromResult(DecisionLogOutcome.Recorded);
        }
    }
}
