using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Loop;

public sealed class LoopTraceCollectorTests
{
    [Fact]
    public void AppendLlmCall_AppendsLlmEntryToTrace()
    {
        var collector = new LoopTraceCollector();

        collector.AppendLlmCall("claude-sonnet", 100, 50, 750);

        var trace = collector.Build();
        trace.Should().HaveCount(1);
        trace[0].Kind.Should().Be(LoopTraceEntryKind.LlmCall);
        trace[0].ModelName.Should().Be("claude-sonnet");
        trace[0].InputTokens.Should().Be(100);
        trace[0].OutputTokens.Should().Be(50);
        trace[0].DurationMs.Should().Be(750);
    }

    [Fact]
    public void AppendToolCall_AppendsToolEntryToTrace()
    {
        var collector = new LoopTraceCollector();

        collector.AppendToolCall("read_file", "{\"path\":\"a.cs\"}", 12, true, null);

        var trace = collector.Build();
        trace.Should().HaveCount(1);
        trace[0].Kind.Should().Be(LoopTraceEntryKind.ToolCall);
        trace[0].ToolName.Should().Be("read_file");
        trace[0].Success.Should().Be(true);
    }

    [Fact]
    public void AppendToolCall_LongArgsJson_TruncatesAt200CharsWithEllipsis()
    {
        var collector = new LoopTraceCollector();
        var longArgs = new string('x', 500);

        collector.AppendToolCall("grep", longArgs, 5, true, null);

        var trace = collector.Build();
        trace[0].ArgsSummary!.Length.Should().Be(200);
        trace[0].ArgsSummary!.Should().EndWith("…");
    }

    [Fact]
    public void AppendToolCall_PreservesLeadingObjectBoundary()
    {
        var collector = new LoopTraceCollector();
        var longArgs = "{" + new string('x', 500);

        collector.AppendToolCall("read_file", longArgs, 5, true, null);

        var trace = collector.Build();
        trace[0].ArgsSummary!.Length.Should().Be(200);
        trace[0].ArgsSummary!.Should().StartWith("{");
        trace[0].ArgsSummary!.Should().EndWith("…");
    }

    [Fact]
    public void EmitLog_WritesStructuredEntryAtInfoLevel()
    {
        var collector = new LoopTraceCollector();
        collector.AppendLlmCall("model-a", 100, 50, 100);
        collector.AppendToolCall("read_file", "{}", 5, true, null);
        var loggerMock = new Mock<ILogger>();

        collector.EmitLog(loggerMock.Object, "test-skill");

        loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Build_ReturnsImmutableSnapshot()
    {
        var collector = new LoopTraceCollector();
        collector.AppendLlmCall("model-a", 1, 2, 3);
        var snapshot1 = collector.Build();

        collector.AppendLlmCall("model-b", 4, 5, 6);
        var snapshot2 = collector.Build();

        snapshot1.Should().HaveCount(1);
        snapshot2.Should().HaveCount(2);
    }
}
