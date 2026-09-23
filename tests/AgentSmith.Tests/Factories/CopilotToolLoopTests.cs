using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-23-4722a: the tool loop stays in agent-smith.
///
/// The session is told about the tools WITHOUT bodies, so it asks us to run them instead of running
/// them itself. Each model call is therefore its own GetResponseAsync, which is what keeps the
/// iteration cap, the rate limiter, the cost events and the run trace re-entering per call rather
/// than once per turn.
/// </summary>
public sealed class CopilotToolLoopTests
{
    private static readonly CopilotSessionRequest Template =
        new(Model: "gpt-5", ReasoningEffort: null, SystemMessage: null, SeatToken: "seat", Tools: []);

    private static CopilotSessionChatClient NewClient(FakeCopilotRuntime runtime) =>
        new(runtime, Template, NullLogger<CopilotSessionChatClient>.Instance);

    private static ChatOptions WithTools(params string[] names) => new()
    {
        Tools = [.. names.Select(n => AIFunctionFactory.Create(() => "ok", n))],
    };

    private static CopilotSessionEvent.ExternalToolRequested Requested(string id, string name, string args = "{}") =>
        new($"req-{id}", id, name, args);

    [Fact]
    public async Task ExternalToolRequested_ReturnsAFunctionCallAndDoesNotExecuteIt()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("let me look", ToolRequestCount: 1),
            Requested("call-1", "read_file", """{"path":"a.cs"}"""));

        var response = await NewClient(runtime).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "read a.cs")], WithTools("read_file"));

        var call = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Single();
        call.Name.Should().Be("read_file");
        call.CallId.Should().Be("call-1");
        call.Arguments!["path"]!.ToString().Should().Be("a.cs");
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
        runtime.Sessions[0].ToolResponses.Should().BeEmpty("the session must not be told a result we have not got");
    }

    [Fact]
    public async Task TwoToolsRequestedInOneMessage_ReturnsBothFunctionCalls()
    {
        // The assistant message says how many to expect; ending on the first would strand the
        // second, and the runtime would wait for a result that never comes.
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("two at once", ToolRequestCount: 2),
            Requested("call-1", "read_file"),
            Requested("call-2", "list_dir"));

        var response = await NewClient(runtime).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "look around")], WithTools("read_file", "list_dir"));

        response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Select(c => c.CallId).Should().Equal("call-1", "call-2");
    }

    [Fact]
    public async Task NextCallCarriesTheToolResults_AnswersEachPendingCallByRequestId()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("working", ToolRequestCount: 2),
            Requested("call-1", "read_file"),
            Requested("call-2", "list_dir"));
        runtime.Script(new CopilotSessionEvent.AssistantMessage("done"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "go")], WithTools("read_file", "list_dir"));

        // What FunctionInvokingChatClient sends back once the response named a ConversationId:
        // only the new tool messages.
        var final = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "file contents")]),
                new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-2", "a.cs b.cs")]),
            ],
            WithTools("read_file", "list_dir"));

        final.Text.Should().Be("done");
        runtime.Sessions.Should().HaveCount(1, "the turn continues in the session that is waiting for it");
        runtime.Sessions[0].ToolResponses.Select(r => r.RequestId).Should().Equal("req-call-1", "req-call-2");
        runtime.Sessions[0].ToolResponses[0].Result.Should().Be("file contents");
        runtime.Sessions[0].Prompts.Should().ContainSingle("a continuation answers the pending call, it does not prompt");
    }

    [Fact]
    public async Task ToolThrew_IsReportedThroughTheErrorArgument()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("working", ToolRequestCount: 1),
            Requested("call-1", "read_file"));
        runtime.Script(new CopilotSessionEvent.AssistantMessage("understood"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "go")], WithTools("read_file"));

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent("call-1", null) { Exception = new InvalidOperationException("no such file") },
            ])],
            WithTools("read_file"));

        var answered = runtime.Sessions[0].ToolResponses.Single();
        answered.Error.Should().Be("no such file");
        answered.Result.Should().BeNull("a failure told as a result would read to the model as an answer");
    }

    [Fact]
    public async Task ToolRoundTrip_EachModelCallIsItsOwnGetResponseAsync()
    {
        // The property the whole phase exists for: two model calls, two responses — so two
        // rate-limit acquisitions, two LlmCall pairs and two trace entries.
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("step one", ToolRequestCount: 1),
            Requested("call-1", "read_file"));
        runtime.Script(new CopilotSessionEvent.AssistantMessage("step two"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        var first = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "go")], WithTools("read_file"));
        var second = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "ok")])], WithTools("read_file"));

        first.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
        second.FinishReason.Should().Be(ChatFinishReason.Stop);
        first.ConversationId.Should().Be(second.ConversationId).And.NotBeNull();
    }

    [Fact]
    public async Task ExternalToolCompletedElsewhere_FailsTheCallLoudly()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantMessage("working", ToolRequestCount: 1),
            new CopilotSessionEvent.ExternalToolCompleted("req-call-1"));

        var act = () => NewClient(runtime).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], WithTools("read_file"));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*req-call-1*");
    }

    [Fact]
    public async Task SessionCreated_DeclaresTheToolsAndAllowsOnlyThem()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("hi"), new CopilotSessionEvent.Idle());

        await NewClient(runtime).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "q")], WithTools("read_file", "list_dir"));

        var request = runtime.Requests.Single();
        request.Tools.Select(t => t.Name).Should().Equal("read_file", "list_dir");
        request.ToolNames.Should().Equal("read_file", "list_dir");
        request.Tools.Should().OnlyContain(t => t.Description != null);
    }

    [Fact]
    public async Task LastIterationWithToolsStripped_KeepsTheSession()
    {
        // FunctionInvokingChatClient removes the declarations for the final pass. Reading that as
        // "the tool set changed" would throw away a live conversation mid-turn.
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("one"), new CopilotSessionEvent.Idle());
        runtime.Script(new CopilotSessionEvent.AssistantMessage("two"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        var history = new List<ChatMessage> { new(ChatRole.User, "go") };
        await client.GetResponseAsync(history, WithTools("read_file"));
        history.Add(new ChatMessage(ChatRole.Assistant, "one"));
        history.Add(new ChatMessage(ChatRole.User, "and now"));
        await client.GetResponseAsync(history, new ChatOptions());

        runtime.Sessions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToolSetChanges_RebuildsTheSession()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("one"), new CopilotSessionEvent.Idle());
        runtime.Script(new CopilotSessionEvent.AssistantMessage("two"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        var history = new List<ChatMessage> { new(ChatRole.User, "go") };
        await client.GetResponseAsync(history, WithTools("read_file"));
        history.Add(new ChatMessage(ChatRole.Assistant, "one"));
        history.Add(new ChatMessage(ChatRole.User, "again"));
        await client.GetResponseAsync(history, WithTools("read_file", "write_file"));

        // A session's tools are fixed when it opens, so a genuinely different set needs a new one.
        runtime.Sessions.Should().HaveCount(2);
        runtime.Requests[1].ToolNames.Should().Equal("read_file", "write_file");
    }

    [Fact]
    public void ADeclarationOnlyTool_IsNotSomethingTheRuntimeWillRun()
    {
        // The SDK decides by asking the declaration for an AIFunction: one that answers is invoked
        // automatically, one that does not is left pending for us. This is the hinge of the phase.
        var real = AIFunctionFactory.Create(() => "ok", "read_file");
        var declared = CopilotToolProjection.From([real]).Single();

        var stripped = new List<AITool> { new PendingToolDeclarationProbe(declared) }[0];
        stripped.GetService(typeof(AIFunction)).Should().BeNull();
        ((AIFunctionDeclaration)stripped).Name.Should().Be("read_file");
        ((AIFunctionDeclaration)stripped).JsonSchema.ToString().Should().Be(real.JsonSchema.ToString());
    }

    /// <summary>Reaches the internal declaration type the runtime is actually handed.</summary>
    private sealed class PendingToolDeclarationProbe(CopilotToolDefinition definition) : AIFunctionDeclaration
    {
        public override string Name { get; } = definition.Name;
        public override string Description { get; } = definition.Description;
        public override System.Text.Json.JsonElement JsonSchema { get; } = definition.Parameters;
    }
}
