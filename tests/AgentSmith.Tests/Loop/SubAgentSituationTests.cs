using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Loop;

/// <summary>
/// 2026-11-09-0620e: a child is told where it works, what it can reach and how long, and its answer comes back in the
/// spawn result, cut at a size a master can read.
/// </summary>
public sealed class SubAgentSituationTests
{
    [Fact]
    public void SubAgentSituation_ReadOnlySurface_SaysTheChildReadsOnly()
    {
        var tools = new[] { AIFunctionFactory.Create((string path) => path, "read_file") };

        SubAgentSituation.Render(["server"], tools, 12).Should().Contain("Your tools read; nothing you do changes a file.")
            .And.NotContain("read and write");
    }

    [Fact]
    public void SubAgentSituation_TwoRepositories_NamesThePrefixAndTheBound()
    {
        var tools = new[] { AIFunctionFactory.Create((string path) => path, "write_file") };

        SubAgentSituation.Render(["worker", "server"], tools, 30).Should()
            .Contain("(`server/...`): server, worker.").And.Contain("read and write").And.Contain("up to 30 tool rounds")
            .And.Contain("Your final message is what your master receives");
    }

    /// <summary>2026-11-10-14a3a: the notice leads; at the end it read like the answer's last line.</summary>
    [Fact]
    public void SubAgentAnswer_LongerThanTheCap_LeadsWithTheNotice()
    {
        var cut = SubAgentAnswer.Condensed(new string('x', SubAgentAnswer.MaxChars + 50), "sa-1");

        cut.Should().StartWith($"[CUT — the first {SubAgentAnswer.MaxChars} of {SubAgentAnswer.MaxChars + 50} characters; "
            + "read_sub_agent_observations(\"sa-1\")").And.EndWith(new string('x', SubAgentAnswer.MaxChars));
        SubAgentAnswer.WasCut(cut).Should().BeTrue();
        SubAgentAnswer.Condensed("short", "sa-1").Should().Be("short");
    }

    [Fact]
    public async Task SpawnAgents_ChildAnswers_ResultCarriesTheAnswer()
    {
        var loop = new AnsweringLoop("The server authenticates by cookie; see API/Extensions/AuthenticationExtension.cs.");
        var runner = new SubAgentRunner(loop, new RecordingEventPublisher(), new LoopLimitsConfig { MaxConcurrentSubAgents = 2 },
            new AgentSmith.Application.Services.Turns.SubAgentActivityFrame(
                new AgentSmith.Application.Services.Turns.AsyncLocalTurnActivityObserverAccessor()),
            NullLogger<SubAgentRunner>.Instance);
        var pipeline = new PipelineContext();
        var context = new SubAgentContext(
            pipeline, new Dictionary<string, ISandbox> { ["server"] = Mock.Of<ISandbox>() },
            PipelineCostTracker.GetOrCreate(pipeline), "run-1", [], new InMemoryChildAnswerStore(),
            new SubAgentBudget(4), new AgentConfig { Type = "azure_openai", Model = "m" }, ChildIterationCeiling: 9);

        var results = await runner.RunAsync(
            [new SubAgentSpec("ServerAuthReader", "Read the server's auth", "find auth", new InheritedContext("goal", "slice"))],
            context, CancellationToken.None);

        results.Single().Answer.Should().Contain("authenticates by cookie");
        loop.SystemPrompt.Should().StartWith("You are ServerAuthReader.").And.Contain("(`server/...`)")
            .And.Contain("up to 9 tool rounds");
    }

    private sealed class AnsweringLoop(string answer) : IAgenticLoopRunner
    {
        public string SystemPrompt { get; private set; } = string.Empty;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            SystemPrompt = request.SystemPrompt;
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)), TimeSpan.FromMilliseconds(1)));
        }
    }
}
