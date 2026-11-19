using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Factories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SubAgents;

/// <summary>
/// 2026-11-18-c8e2a: a master and its children through the real loop runner and chat-client factory, each agent
/// scripted on its own: a background child is written to mid-way and reads it with its next tool result, a finished
/// child's answer reaches the master with its next tool result, and a continued child keeps its conversation.
/// </summary>
public sealed class SubAgentTeamLoopTests
{
    private const string Child = "BuilderChild";

    [Fact]
    public async Task BackgroundChild_MasterWritesMidWay_ChildReadsItWithItsNextToolResult()
    {
        var reading = new TaskCompletionSource();
        var posted = new TaskCompletionSource();
        var provider = new AgentScripts();
        provider.Script(Child,
            _ => Call("read_file", new() { ["path"] = "a" }),
            messages => Text(LastToolResult(messages).Contains("use the other file") ? "saw it" : "missed it"));
        var rig = new Rig(provider, ReadTool(async () => { reading.TrySetResult(); await posted.Task; return "file text"; }));

        var member = rig.Runner.Start([Spec(Child)], rig.Context, (_, _) => Task.CompletedTask, CancellationToken.None).Single();
        await reading.Task;
        member.TryPost("use the other file").Should().BeTrue();
        posted.SetResult();
        await member.Pass;

        provider.Requests(Child)[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result!.ToString()
            .Should().Contain(SubAgentMember.LeadHeading).And.Contain("use the other file");
        rig.Team.Notices.Drain().Single().Text.Should().Contain("saw it").And.Contain(member.Id);
    }

    [Fact]
    public async Task Master_StartsWaitsAndContinuesAChild_ChildKeepsItsConversation()
    {
        var provider = new AgentScripts();
        string? childId = null;
        provider.Script("lead",
            _ => Call("spawn_agents", new() { ["background"] = true, ["tasks"] = Tasks(Child) }),
            messages => { childId = IdIn(LastToolResult(messages)); return Call("send_message", new() { ["wait"] = true }); },
            messages => ToolResults(messages).Contains("built the endpoint")
                ? Call("send_message", new() { ["agent_id"] = childId, ["text"] = "the done list wants a test too" })
                : Text("no notice arrived"),
            _ => Call("send_message", new() { ["wait"] = true }),
            messages => Text(LastToolResult(messages).Contains("test added") ? "lead satisfied" : "no second notice"));
        provider.Script(Child,
            _ => Text("built the endpoint"),
            messages => Text(messages.Any(m => m.Text.Contains("built the endpoint"))
                && messages[^1].Text.Contains("the done list wants a test too") ? "test added" : "lost my context"));
        var rig = new Rig(provider, ReadTool(() => Task.FromResult("file text")));

        var result = await rig.Loop.RunAsync(new AgenticLoopRequest(
            ToolLoopErrorToleranceSupport.Agent(), TaskType.Primary, "You are lead.", "lead the spec", rig.MasterTools()),
            CancellationToken.None);

        result.Response.Text.Should().Be("lead satisfied");
        provider.Requests(Child).Should().HaveCount(2, "the child ran once and was continued once");
        rig.Publisher.Events.OfType<SubAgentSpawnedEvent>().Should().ContainSingle();
        rig.Publisher.Events.OfType<SubAgentCompletedEvent>().Should().HaveCount(2);
        rig.Publisher.Events.OfType<SubAgentMessagedEvent>().Should().ContainSingle() // 2026-11-18-c8e2d
            .Which.Should().Match<SubAgentMessagedEvent>(e => e.Continued && e.Text == "the done list wants a test too");
    }

    [Fact]
    public async Task Lead_SendsWorkBack_ChildFixesIt_ReportsAndFinishesWithFollowUp()
    {
        const string report = "## Delivered\n- the endpoint returns 404, tested\n\n## Open\n- exports stream CSV needs paging";
        var finish = new AgentSmith.Application.Services.Tools.FinishSpecToolHost();
        var provider = new AgentScripts();
        string? childId = null;
        provider.Script("lead",
            _ => Call("spawn_agents", new() { ["background"] = true, ["tasks"] = Tasks(Child) }),
            messages => { childId = IdIn(LastToolResult(messages)); return Call("send_message", new() { ["wait"] = true }); },
            messages => ToolResults(messages).Contains("404 test")
                ? Text("sent nothing back")
                : Call("send_message", new() { ["agent_id"] = childId, ["text"] = "the done list wants the 404 tested" }),
            _ => Call("send_message", new() { ["wait"] = true }),
            messages => LastToolResult(messages).Contains("404 test added")
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent(report), new FunctionCallContent("f1", "finish_spec",
                    new Dictionary<string, object?>
                    {
                        ["status"] = "green", ["summary"] = "the 404 endpoint",
                        ["follow_ups"] = System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { goal = "Exports stream CSV over paging", why = "paging is its own design" } }),
                    })]))
                : Text("the child never fixed it"),
            _ => Text(""));
        provider.Script(Child,
            _ => Text("built the endpoint"),
            messages => Text(messages[^1].Text.Contains("404 tested") ? "404 test added" : "no feedback"));
        var rig = new Rig(provider, ReadTool(() => Task.FromResult("file text")));

        var result = await rig.Loop.RunAsync(new AgenticLoopRequest(
            ToolLoopErrorToleranceSupport.Agent(), TaskType.Primary, "You are lead.", "lead the spec",
            [.. rig.MasterTools(), .. finish.GetTools(null, null)]),
            CancellationToken.None);

        finish.Verification!.FollowUps.Should().ContainSingle().Which.Goal.Should().Be("Exports stream CSV over paging");
        AgentSmith.Application.Services.Handlers.MasterReport.Of([.. result.Response.Messages]).Should().Be(report);
    }

    [Fact]
    public async Task StopAsync_ARunningChild_IsStoppedAndPostsNoNotice()
    {
        var reading = new TaskCompletionSource();
        var provider = new AgentScripts();
        provider.Script(Child, _ => Call("read_file", new() { ["path"] = "a" }), _ => Text("never"));
        var rig = new Rig(provider, AIFunctionFactory.Create(async (string path, CancellationToken ct) =>
        {
            reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return path;
        }, "read_file"));

        var member = rig.Runner.Start([Spec(Child)], rig.Context, (_, _) => Task.CompletedTask, CancellationToken.None).Single();
        await reading.Task;
        await rig.Team.StopAsync();

        member.IsRunning.Should().BeFalse();
        rig.Team.Notices.Count.Should().Be(0);
        rig.Publisher.Events.OfType<SubAgentCompletedEvent>().Single().Status.Should().Be(nameof(SubAgentStatus.Stopped));
    }

    private static SubAgentSpec Spec(string name) => new(name, "build the endpoint", "build it", new InheritedContext("goal", "slice"));

    private static JsonElement Tasks(string name) => JsonSerializer.SerializeToElement(new[]
    {
        new { name, activity = "build the endpoint", task_description = "build it",
              inherited_context = new { pipeline_goal = "goal", prior_context_slice = "slice" } },
    });

    private static AIFunction ReadTool(Func<Task<string>> body) =>
        AIFunctionFactory.Create(async (string path) => await body(), "read_file");

    private static ChatResponse Call(string name, Dictionary<string, object?> args) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"c-{Guid.NewGuid():N}", name, args)]));

    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static string LastToolResult(IReadOnlyList<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault()?.Result?.ToString() ?? "";

    private static string ToolResults(IReadOnlyList<ChatMessage> messages) => string.Join("\n",
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString()));

    private static string IdIn(string spawnResult) =>
        JsonDocument.Parse(spawnResult.Split("\n\n")[0]).RootElement.GetProperty("results")[0].GetProperty("sub_agent_id").GetString()!;

    private sealed class Rig
    {
        public Rig(AgentScripts provider, AIFunction childTool)
        {
            Loop = new AgenticLoopRunner(ToolLoopErrorToleranceSupport.LiveFactory(provider), EventTestStubs.RunContext,
                NullLogger<AgenticLoopRunner>.Instance);
            Runner = SubAgentRunnerTests.BuildRunner(Loop, publisher: Publisher);
            var pipeline = new PipelineContext();
            Context = new SubAgentContext(pipeline, new Dictionary<string, ISandbox>(), PipelineCostTracker.GetOrCreate(pipeline),
                "run-test", [childTool], new SubAgentBudget(maxPerRun: 10), ToolLoopErrorToleranceSupport.Agent(), Team: Team);
        }

        public SubAgentTeam Team { get; } = new(NullLogger.Instance);
        public RecordingEventPublisher Publisher { get; } = new();
        public AgenticLoopRunner Loop { get; }
        public SubAgentRunner Runner { get; }
        public SubAgentContext Context { get; }

        public IList<AITool> MasterTools()
        {
            var decisions = new Handlers.MasterHandlerFixture.NoOpDecisionLogger();
            var tools = new SpawnAgentToolHost(Runner, Context.Budget, new SubAgentNameValidator(), decisions, Context)
                .GetTools(null, null)
                .Concat(new SubAgentTeamToolHost(Team, Runner, Context, decisions, new SubAgentEvents(Publisher)).GetTools(null, null));
            return [.. tools.Select(f => (AITool)new MidTurnMessageAIFunction(f, Team.Notices))];
        }
    }

    /// <summary>One script per agent, chosen by the name its system prompt opens with; each keeps its requests.</summary>
    private sealed class AgentScripts : IChatClient
    {
        private readonly Dictionary<string, Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>>> _scripts = [];
        private readonly Dictionary<string, List<List<ChatMessage>>> _requests = [];
        private readonly Lock _gate = new();

        public void Script(string agent, params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] steps) =>
            _scripts[agent] = new Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>>(steps);

        public IReadOnlyList<List<ChatMessage>> Requests(string agent)
        {
            lock (_gate) return [.. _requests.GetValueOrDefault(agent) ?? []];
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var list = messages.ToList();
            var agent = _scripts.Keys.First(name => list[0].Text.StartsWith($"You are {name}", StringComparison.Ordinal));
            Func<IReadOnlyList<ChatMessage>, ChatResponse> step;
            lock (_gate)
            {
                (_requests.TryGetValue(agent, out var seen) ? seen : _requests[agent] = []).Add(list);
                step = _scripts[agent].Dequeue();
            }
            return Task.FromResult(step(list));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
