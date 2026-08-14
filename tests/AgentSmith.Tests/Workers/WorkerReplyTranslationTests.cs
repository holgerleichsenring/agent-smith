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
    public void Parse_Prose_IsTheAnswer_BecauseAnsweringNeedsNoEnvelope()
    {
        // p0416, first live run: the CLI emitted an empty envelope and then the real
        // answer as prose, and the run died on a correct answer. An agent narrates;
        // acting needs the envelope, answering does not.
        NewParser().TryParse("Sure! I'll write the file now.", out var reply, out var problem)
            .Should().BeTrue();

        problem.Should().BeNull();
        reply.Text.Should().Be("Sure! I'll write the file now.");
        reply.ToolCalls.Should().BeNullOrEmpty("prose is an answer, never an action");
    }

    [Fact]
    public void Parse_EmptyEnvelopeThenTheRealAnswer_TakesTheAnswer()
    {
        // Verbatim shape from run ba2e step 12, which failed the whole pipeline.
        const string stdout = """
            {"text": "", "tool_calls": []}

            Based on the evidence, the final JSON:
            {"primary_language": "csharp"}
            """;

        NewParser().TryParse(stdout, out var reply, out _).Should().BeTrue();

        reply.Text.Should().Contain("primary_language",
            "an envelope carrying nothing is narration; the substance after it is the reply");
    }

    [Fact]
    public void Parse_StructuredAnswer_IsNotMistakenForAnEnvelope()
    {
        // Run 6bad died twice on this: a structured-output call answers WITH a JSON
        // object of its own, and deserialising that into a WorkerReply drops every
        // field and looks like an empty envelope. Only the named keys tell the two
        // JSON contracts apart.
        const string stdout = """{"primary_language": "csharp", "frameworks": [".NET 8"]}""";

        NewParser().TryParse(stdout, out var reply, out _).Should().BeTrue();

        reply.Text.Should().Contain("primary_language");
        reply.ToolCalls.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Parse_ActingEnvelopeAmidNarration_StillActs()
    {
        const string stdout = """
            I'll read the manifest first.
            {"tool_calls": [{"name": "read_file", "arguments": {"path": "a.csproj"}}]}
            """;

        NewParser().TryParse(stdout, out var reply, out _).Should().BeTrue();

        reply.ToolCalls.Should().ContainSingle().Which.Name.Should().Be("read_file",
            "a tool call must never be swallowed by the prose around it");
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
