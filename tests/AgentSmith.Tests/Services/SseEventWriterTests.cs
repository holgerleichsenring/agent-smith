using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SseEventWriterTests
{
    [Fact]
    public void Format_ProgressMessage_EmittedAsProgressEvent()
    {
        var msg = BusMessage.Progress("job-1", step: 3, total: 10, "Building");

        var sse = SseEventWriter.Format(msg);

        sse.Should().StartWith("event: progress\n");
        sse.Should().Contain("\"step\":3");
        sse.Should().Contain("\"total\":10");
        sse.Should().EndWith("\n\n");
    }

    [Fact]
    public void Format_DoneMessage_EmittedAsDoneEventWithPrUrl()
    {
        var msg = BusMessage.Done("job-1", prUrl: "https://example.com/pr/1", "all green");

        var sse = SseEventWriter.Format(msg);

        sse.Should().StartWith("event: done\n");
        sse.Should().Contain("\"run_id\":\"job-1\"");
        sse.Should().Contain("\"pr_url\":\"https://example.com/pr/1\"");
        sse.Should().Contain("\"summary\":\"all green\"");
    }

    [Fact]
    public void Format_ErrorMessage_EmittedAsErrorEventWithContext()
    {
        var msg = BusMessage.Error("job-1", "boom", step: 2, total: 5, stepName: "Test");

        var sse = SseEventWriter.Format(msg);

        sse.Should().StartWith("event: error\n");
        sse.Should().Contain("\"run_id\":\"job-1\"");
        sse.Should().Contain("\"error_context\":\"boom\"");
    }

    [Fact]
    public void Format_DetailMessage_EmittedAsToolCallWithTruncation()
    {
        var longText = new string('x', 300);
        var msg = BusMessage.Detail("job-1", longText);

        var sse = SseEventWriter.Format(msg);

        sse.Should().StartWith("event: tool_call\n");
        sse.Should().Contain("\"tool_name\":\"detail\"");
        // args_preview is truncated to 200 chars + "..." marker
        sse.Should().Contain(new string('x', 200) + "...");
    }
}
