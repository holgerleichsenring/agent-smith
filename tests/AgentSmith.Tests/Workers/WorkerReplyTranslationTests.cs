using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0416: parsing and translating the worker's answer is the translation layer that makes
/// an external agent usable as a MODEL. It is strict on purpose: an answer that is not the
/// agreed shape fails with a reason, because a silent degrade to "the worker said some
/// text" would let a broken bridge look like a model that merely chose to talk.
/// </summary>
public sealed class WorkerReplyTranslationTests
{
    private static WorkerReplyParser NewParser() => new(new WorkerJsonFormat());

    private static WorkerRequest RequestOffering(params string[] tools) =>
        new("agentsmith.worker/1", "req-1", "run-42", 7, "coding-master", "Implementation",
            "primary", "external_worker", "sonnet", DateTimeOffset.UtcNow, [],
            [.. tools.Select(t => new WorkerToolDefinition(t, null, null))],
            new WorkerRequestOptions());

    [Fact]
    public void Parse_PlainJsonObject_YieldsTheReply()
    {
        NewParser().TryParse("""{"text":"done"}""", out var reply, out var problem)
            .Should().BeTrue();
        problem.Should().BeNull();
        reply.Text.Should().Be("done");
    }

    [Fact]
    public void Parse_FencedJson_IsUnwrapped_TheOneToleratedWrapper()
    {
        var stdout = "```json\n{\"tool_calls\":[{\"name\":\"write_file\",\"arguments\":{\"path\":\"a\"}}]}\n```";

        NewParser().TryParse(stdout, out var reply, out var problem).Should().BeTrue();

        problem.Should().BeNull();
        reply.ToolCalls.Should().ContainSingle().Which.Name.Should().Be("write_file");
    }

    [Fact]
    public void Parse_Prose_FailsWithAReason_NeverDegradesToText()
    {
        NewParser().TryParse("Sure! I'll write the file now.", out _, out var problem)
            .Should().BeFalse();

        problem.Should().Contain("not the agreed JSON object").And.Contain("Sure!");
    }

    [Fact]
    public void Parse_EmptyAnswer_FailsWithAReason()
    {
        NewParser().TryParse("   ", out _, out var problem).Should().BeFalse();
        problem.Should().Contain("empty answer");
    }

    [Fact]
    public void Translate_ToolCall_BecomesAFunctionCallTheLoopCanExecute()
    {
        var reply = new WorkerReply(Text: "Writing the guard.", ToolCalls:
            [new WorkerToolCall("write_file",
                JsonDocument.Parse("""{"path":"src/A.cs","content":"// x"}""").RootElement)]);

        new WorkerReplyTranslator()
            .TryTranslate(reply, RequestOffering("write_file"), out var response, out var problem)
            .Should().BeTrue();

        problem.Should().BeNull();
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
        var call = response.Messages[0].Contents.OfType<FunctionCallContent>().Should().ContainSingle().Subject;
        call.Name.Should().Be("write_file");
        call.CallId.Should().NotBeNullOrEmpty("the framework owns call correlation, not the worker");
        call.Arguments!["path"]!.ToString().Should().Be("src/A.cs");
        response.Text.Should().Contain("Writing the guard.");
    }

    [Fact]
    public void Translate_TextOnly_StopsTheTurn()
    {
        new WorkerReplyTranslator()
            .TryTranslate(new WorkerReply(Text: "all green"), RequestOffering("write_file"),
                out var response, out _)
            .Should().BeTrue();

        response.FinishReason.Should().Be(ChatFinishReason.Stop);
        response.Text.Should().Be("all green");
    }

    [Fact]
    public void Translate_ToolThatWasNeverOffered_FailsNamingWhatWasOffered()
    {
        var reply = new WorkerReply(ToolCalls:
            [new WorkerToolCall("rm_rf", JsonDocument.Parse("{}").RootElement)]);

        new WorkerReplyTranslator()
            .TryTranslate(reply, RequestOffering("write_file"), out _, out var problem)
            .Should().BeFalse();

        problem.Should().Contain("rm_rf").And.Contain("write_file");
    }

    [Fact]
    public void Translate_ArgumentsThatAreNotAnObject_Fails()
    {
        var reply = new WorkerReply(ToolCalls:
            [new WorkerToolCall("write_file", JsonDocument.Parse("\"src/A.cs\"").RootElement)]);

        new WorkerReplyTranslator()
            .TryTranslate(reply, RequestOffering("write_file"), out _, out var problem)
            .Should().BeFalse();

        problem.Should().Contain("not a JSON object");
    }

    [Fact]
    public void Translate_NeitherTextNorToolCalls_Fails()
    {
        new WorkerReplyTranslator()
            .TryTranslate(new WorkerReply(), RequestOffering("write_file"), out _, out var problem)
            .Should().BeFalse();

        problem.Should().Contain("neither text nor tool calls");
    }

    [Fact]
    public void Translate_WorkerReportedError_FailsInsteadOfBecomingAModelAnswer()
    {
        new WorkerReplyTranslator()
            .TryTranslate(new WorkerReply(Error: "session limit reached"),
                RequestOffering("write_file"), out _, out var problem)
            .Should().BeFalse();

        problem.Should().Contain("session limit reached");
    }
}
