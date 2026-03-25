using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class MultiOutputTests
{
    [Fact]
    public async Task DeliverFindingsHandler_MultipleFormats_ExecutesAll()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SummaryOutputStrategy>("summary");
        var sp = services.BuildServiceProvider();

        var handler = new DeliverFindingsHandler(sp, NullLogger<DeliverFindingsHandler>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "**1. Test Finding**\n- severity: HIGH\n- confidence: 9");
        var context = new DeliverFindingsContext(["console", "summary"], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("console");
        result.Message.Should().Contain("summary");
    }

    [Fact]
    public async Task DeliverFindingsHandler_UnknownFormat_SkipsAndContinues()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        var sp = services.BuildServiceProvider();

        var handler = new DeliverFindingsHandler(sp, NullLogger<DeliverFindingsHandler>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "No findings");
        var context = new DeliverFindingsContext(["nonexistent", "console"], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("console");
        result.Message.Should().NotContain("nonexistent");
    }

    [Fact]
    public async Task DeliverFindingsHandler_AllUnknown_ReturnsFail()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var handler = new DeliverFindingsHandler(sp, NullLogger<DeliverFindingsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DeliverFindingsContext(["xyz"], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task DeliverFindingsHandler_OutputDir_PassedToPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        var sp = services.BuildServiceProvider();

        var handler = new DeliverFindingsHandler(sp, NullLogger<DeliverFindingsHandler>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConsolidatedPlan, "test");
        var context = new DeliverFindingsContext(["console"], "/tmp/test-output", pipeline);

        await handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<string>(ContextKeys.OutputDir, out var dir).Should().BeTrue();
        dir.Should().Be("/tmp/test-output");
    }
}

public sealed class SummaryOutputStrategyTests
{
    [Fact]
    public void ParseFindings_NumberedPattern_ExtractsTitleAndSeverity()
    {
        var text = """
            ## Critical Issues (HIGH Severity)

            **1. OAuth2 Configuration Missing**
            - severity: HIGH
            - confidence: 9

            **2. Missing Rate Limiting**
            - severity: MEDIUM
            - confidence: 7
            """;

        var findings = SummaryOutputStrategy.ParseFindings(text);

        findings.Should().HaveCount(2);
        findings[0].Title.Should().Be("OAuth2 Configuration Missing");
        findings[0].Severity.Should().Be("HIGH");
        findings[0].Confidence.Should().Be(9);
        findings[1].Title.Should().Be("Missing Rate Limiting");
        findings[1].Severity.Should().Be("MEDIUM");
        findings[1].Confidence.Should().Be(7);
    }

    [Fact]
    public void ParseFindings_EmptyText_ReturnsEmpty()
    {
        var findings = SummaryOutputStrategy.ParseFindings("");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_NoPatternMatch_ReturnsEmpty()
    {
        var findings = SummaryOutputStrategy.ParseFindings("Just plain text with no findings");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_SectionHeaderSets_Severity()
    {
        var text = """
            ## Critical Issues

            **1. Critical Finding**
            - confidence: 10

            ## Medium Severity Issues

            **2. Medium Finding**
            - confidence: 5
            """;

        var findings = SummaryOutputStrategy.ParseFindings(text);

        findings.Should().HaveCount(2);
        findings[0].Severity.Should().Be("CRITICAL");
        findings[1].Severity.Should().Be("MEDIUM");
    }
}
