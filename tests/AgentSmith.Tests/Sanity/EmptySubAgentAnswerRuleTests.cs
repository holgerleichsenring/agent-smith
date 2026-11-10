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

    /// <summary>2026-11-10-14a3a: an answer comes back whole — a part is never useful; only a runaway is cut, saying so first.</summary>
    [Fact]
    public async Task SpawnAgents_LongAnswer_ComesBackWhole_ARunawayIsMarkedFirst()
    {
        var whole = await Spawn(new string('a', 20000));
        var runaway = await Spawn(new string('b', SubAgentAnswer.Ceiling + 10));

        whole.Should().Be(new string('a', 20000));
        runaway.Should().StartWith("[RUNAWAY — this answer ran to").And.EndWith(new string('b', SubAgentAnswer.Ceiling));
    }

    private static async Task<string> Spawn(string answer)
    {
        var host = new SpawnAgentToolHost(SubAgentRunnerTests.BuildRunner(new StubLoopRunner(answer: answer)),
            new SubAgentBudget(5), new SubAgentNameValidator(), new RecordingDecisionLogger(), SubAgentRunnerTests.BuildContext());
        var result = (await host.GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, CancellationToken.None))!;
        return JsonDocument.Parse(((JsonElement)result).GetString()!).RootElement
            .GetProperty("results")[0].GetProperty("answer").GetString()!;
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
