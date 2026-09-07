using AgentSmith.Contracts.Json;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// Run 0c88 (2026-09-07): the master edited tsconfig.build.json and the edit's
/// old_string opened a JSON block. A brace-count that does not know it is inside a
/// string literal never saw the envelope close, so a valid tool-call envelope was
/// read as narration — twice — and the master stopped "idle with work still open".
/// The payload below is the reply as the worker wrote it.
/// </summary>
public sealed class WorkerReplyBracesInStringsTests
{
    private const string EditWithOpenBrace =
        """
        {"tool_calls": [{"name": "run_command", "arguments": {"command": "cd backend && ls", "repo": "node-service-template", "timeout_seconds": 15}}, {"name": "edit", "arguments": {"path": "node-service-template/backend/tsconfig.build.json", "old_string": "{\n    \"extends\": \"./tsconfig.json\",\n    \"exclude\": [\"node_modules\", \"dist\"]", "new_string": "{\n    \"extends\": \"./tsconfig.json\",\n    \"compilerOptions\": {\n        \"rootDir\": \"./source\"\n    },\n    \"exclude\": [\"node_modules\", \"dist\"]"}}]}
        """;

    [Fact]
    public void Envelope_AnEditWhoseStringOpensABrace_StillCarriesItsToolCalls()
    {
        var parser = new WorkerReplyParser(new WorkerJsonFormat());

        parser.TryParse(EditWithOpenBrace, out var reply, out var problem).Should().BeTrue();

        problem.Should().BeNull();
        reply.ToolCalls.Should().HaveCount(2);
        reply.ToolCalls![1].Name.Should().Be("edit");
    }

    [Fact]
    public void Envelope_FramedByNarrationThatQuotes_IsStillFound()
    {
        var parser = new WorkerReplyParser(new WorkerJsonFormat());
        var reply = "Continuing \"adapt-source\": fixing tsconfig (TS5011).\n" + EditWithOpenBrace;

        parser.TryParse(reply, out var parsed, out _).Should().BeTrue();

        parsed.ToolCalls.Should().HaveCount(2);
    }

    [Fact]
    public void Envelope_AnEscapedQuoteInsideAString_DoesNotEndTheString()
    {
        var spans = JsonObjectSpans.Balanced(
            """{"text": "say \"{\" and go on", "tool_calls": []}""").ToList();

        spans.Should().ContainSingle().Which.Should().EndWith("[]}");
    }
}
