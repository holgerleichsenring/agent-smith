using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0416: the worker stands where the provider stands, so the request it gets must be
/// what the provider would have got. These tests are the guard on that claim — if the
/// bridge ever starts summarising the history or dropping the tool schemas, the run under
/// test stops being the run that ships.
/// </summary>
public sealed class WorkerRequestCompositionTests
{
    private static WorkerRequestComposer NewComposer()
    {
        var json = new WorkerJsonFormat();
        return new WorkerRequestComposer(new WorkerMessageMapper(json), new WorkerOptionsMapper());
    }

    private static WorkerCallIdentity Identity() =>
        new("run-42", 7, "coding-master", "Implementation", "primary", "external_worker", "sonnet");

    [Fact]
    public void Bridge_PublishesTheSameRequestAProviderWouldReceive()
    {
        var tool = AIFunctionFactory.Create(
            (string path, string content) => "ok", "write_file", "Writes a file");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are the coding master."),
            new(ChatRole.User, "Fix the empty-body case."),
            new(ChatRole.Assistant, [new FunctionCallContent(
                "call_1", "write_file", new Dictionary<string, object?> { ["path"] = "src/A.cs" })]),
            new(ChatRole.Tool, [new FunctionResultContent("call_1", "written")]),
        };

        var request = NewComposer().Compose(
            messages, new ChatOptions { Tools = [tool], MaxOutputTokens = 8192 }, Identity());

        request.Protocol.Should().Be("agentsmith.worker/1");
        request.RunId.Should().Be("run-42");
        request.StepIndex.Should().Be(7);
        request.Role.Should().Be("coding-master");
        request.Phase.Should().Be("Implementation");

        request.Messages.Select(m => m.Role).Should().Equal("system", "user", "assistant", "tool");
        request.Messages[0].Content[0].Text.Should().Be("You are the coding master.");
        request.Messages[2].Content[0].Type.Should().Be("tool_call");
        request.Messages[2].Content[0].Name.Should().Be("write_file");
        request.Messages[2].Content[0].Arguments!.Value.GetProperty("path").GetString()
            .Should().Be("src/A.cs", "prior tool calls carry their arguments, not just their names");
        request.Messages[3].Content[0].Type.Should().Be("tool_result");
        request.Messages[3].Content[0].Result.Should().Be("written",
            "a worker that cannot see what its last tool returned is answering a different call");

        request.Tools.Should().ContainSingle().Which.Name.Should().Be("write_file");
        var schema = request.Tools[0].InputSchema!.Value;
        schema.GetProperty("properties").TryGetProperty("path", out _).Should().BeTrue(
            "the tool schema is what lets the worker answer with executable arguments");
        request.Options.MaxOutputTokens.Should().Be(8192);
    }

    [Fact]
    public void Compose_OptionTheEnvelopeCannotCarry_IsNamedInNotRendered()
    {
        var request = NewComposer().Compose(
            [new ChatMessage(ChatRole.User, "hi")],
            new ChatOptions { Seed = 7, TopK = 3 },
            Identity());

        request.Options.NotRendered.Should().Contain("Seed").And.Contain("TopK",
            "a gap between what the provider would see and what the worker sees is declared "
            + "in the payload, never left to a comment");
    }

    [Fact]
    public void Compose_UnrenderableContent_IsDeclaredUnsupported_NotSilentlyDropped()
    {
        var message = new ChatMessage(ChatRole.User,
            [new TextContent("look at this"), new DataContent(new byte[] { 1, 2, 3 }, "image/png")]);

        var request = NewComposer().Compose([message], new ChatOptions(), Identity());

        request.Messages[0].Content.Should().HaveCount(2, "the part is declared, not removed");
        request.Messages[0].Content[1].Type.Should().Be("unsupported");
        request.Messages[0].Content[1].ClrType.Should().Be(nameof(DataContent));
    }

    [Fact]
    public void Compose_NoTools_YieldsAnEmptyToolListRatherThanNull()
    {
        var request = NewComposer().Compose(
            [new ChatMessage(ChatRole.User, "hi")], chatOptions: null, Identity());

        request.Tools.Should().BeEmpty();
        request.Options.MaxOutputTokens.Should().BeNull();
    }

    [Fact]
    public void Render_PutsTheToolSchemaAndTheAnswerContractInTheWorkerPrompt()
    {
        var json = new WorkerJsonFormat();
        var tool = AIFunctionFactory.Create((string path) => "ok", "read_file", "Reads a file");
        var request = NewComposer().Compose(
            [new ChatMessage(ChatRole.User, "hi")], new ChatOptions { Tools = [tool] }, Identity());

        var prompt = new WorkerPromptRenderer(json).Render(request);

        prompt.Should().Contain("read_file").And.Contain("input_schema");
        prompt.Should().Contain("tool_calls", "the worker must know how to answer with an action");
        prompt.Should().Contain("Only the tools listed",
            "the worker answers as the model, it does not use its own tools");
        var envelope = prompt[(prompt.LastIndexOf("REQUEST", StringComparison.Ordinal) + 7)..];
        JsonDocument.Parse(envelope).RootElement
            .GetProperty("run_id").GetString().Should().Be("run-42",
            "the envelope is machine-readable — it is the payload p0166e will carry over MCP");
    }
}
