using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ConsoleOutputStructuredFallbackTests
{
    private readonly ConsoleOutputStrategy _sut = new(NullLogger<ConsoleOutputStrategy>.Instance);

    [Fact]
    public async Task Deliver_StructuredPipeline_NoFindings_PrintsShortPointer()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineTypeName, PipelineType.Structured);
        pipeline.Set(ContextKeys.ConsolidatedPlan, BuildLargeMarkdown(28_000));
        var context = new OutputContext("api-scan", null, [], null, "./test-output", pipeline);

        var output = await CaptureConsoleAsync(() => _sut.DeliverAsync(context));

        output.Should().Contain("see file output");
        output.Should().NotContain("# Security Scan Results");
        output.Length.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task Deliver_DiscussionPipeline_NoFindings_PrintsConsolidatedPlan()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineTypeName, PipelineType.Discussion);
        pipeline.Set(ContextKeys.ConsolidatedPlan, "# Legal Review\n\nFull analysis text.");
        var context = new OutputContext("legal", null, [], null, "./test-output", pipeline);

        var output = await CaptureConsoleAsync(() => _sut.DeliverAsync(context));

        output.Should().Contain("# Legal Review");
        output.Should().Contain("Full analysis text.");
    }

    [Fact]
    public async Task Deliver_StructuredPipeline_WithFindings_PrintsCompactList()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineTypeName, PipelineType.Structured);
        pipeline.Set(ContextKeys.ConsolidatedPlan, BuildLargeMarkdown(28_000));
        var findings = new List<Finding>
        {
            new("HIGH", "/api/login", 0, null, "Unauthenticated login enumeration", "...", 9),
        };
        var context = new OutputContext("api-scan", null, findings, null, "./test-output", pipeline);

        var output = await CaptureConsoleAsync(() => _sut.DeliverAsync(context));

        output.Should().Contain("[HIGH]");
        output.Should().Contain("Unauthenticated login enumeration");
        output.Should().NotContain("# Security Scan Results");
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task> action)
    {
        var original = Console.Out;
        await using var capture = new StringWriter();
        Console.SetOut(capture);
        try { await action(); }
        finally { Console.SetOut(original); }
        return capture.ToString();
    }

    private static string BuildLargeMarkdown(int targetChars)
    {
        const string line = "# Security Scan Results\n\nLong dump line that should not appear on console.\n";
        var multiplier = (targetChars / line.Length) + 1;
        return string.Concat(Enumerable.Repeat(line, multiplier));
    }
}
