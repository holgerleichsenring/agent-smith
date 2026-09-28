using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Output;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class DeliverOutputHandlerTests : IDisposable
{
    private const string Analysis = "# Risk Assessment\nHigh risk clause found.";
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), $"ast-deliver-{Guid.NewGuid():N}");
    private readonly RecordingStrategy _console = new();
    private readonly DeliverOutputHandler _sut;

    public DeliverOutputHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IOutputStrategy>("console", _console);
        services.AddKeyedSingleton<IOutputStrategy>(
            "markdown", new MarkdownOutputStrategy(NullLogger<MarkdownOutputStrategy>.Instance));
        _sut = new DeliverOutputHandler(
            services.BuildServiceProvider(),
            new OutputDirectoryResolver(NullLogger<OutputDirectoryResolver>.Instance),
            NullLogger<DeliverOutputHandler>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task DeliverOutput_LegalRun_ConsoleRendersMasterAnalysis()
    {
        var pipeline = PipelineWith(
            new CodeChange(new FilePath(".agentsmith/runs/r1/plan.md"), "the plan", "create"),
            new CodeChange(new FilePath("analysis.md"), Analysis, "create"));

        var result = await _sut.ExecuteAsync(
            new DeliverOutputContext("legal", "console", null, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _console.Delivered!.ReportMarkdown.Should().Be(Analysis);
    }

    [Fact]
    public async Task DeliverOutput_Markdown_WritesAnalysisToRequestedOutputDir()
    {
        var pipeline = PipelineWith(new CodeChange(new FilePath("analysis.md"), Analysis, "create"));

        var result = await _sut.ExecuteAsync(
            new DeliverOutputContext("legal", "markdown", _outputDir, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var written = await File.ReadAllTextAsync(Path.Combine(_outputDir, "findings.md"));
        written.Should().Contain(Analysis);
    }

    [Fact]
    public async Task DeliverOutput_NoMasterDocument_FailsNamingTheMissingReport()
    {
        var pipeline = PipelineWith(
            new CodeChange(new FilePath(".agentsmith/runs/r1/decisions.md"), "decisions", "create"));

        var result = await _sut.ExecuteAsync(
            new DeliverOutputContext("legal", "console", null, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No analysis report");
        _console.Delivered.Should().BeNull();
    }

    [Fact]
    public async Task DeliverOutput_UnregisteredFormat_Fails()
    {
        var pipeline = PipelineWith(new CodeChange(new FilePath("analysis.md"), Analysis, "create"));

        var result = await _sut.ExecuteAsync(
            new DeliverOutputContext("legal", "file", null, pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Unknown output format: 'file'");
    }

    private static PipelineContext PipelineWith(params CodeChange[] changes)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)changes);
        return pipeline;
    }

    private sealed class RecordingStrategy : IOutputStrategy
    {
        public OutputContext? Delivered { get; private set; }
        public string ProviderType => "console";

        public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
        {
            Delivered = context;
            return Task.CompletedTask;
        }
    }
}
