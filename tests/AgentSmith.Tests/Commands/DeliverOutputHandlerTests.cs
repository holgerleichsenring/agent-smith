using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class DeliverOutputHandlerTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), $"ast-deliver-{Guid.NewGuid():N}");
    private readonly DeliverOutputHandler _sut = new(
        new ServiceCollection().BuildServiceProvider(),
        NullLogger<DeliverOutputHandler>.Instance);

    public DeliverOutputHandlerTests()
    {
        Directory.CreateDirectory(Path.Combine(_baseDir, "inbox"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "processing"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_WritesAnalysisToOutbox_ArchivesSource()
    {
        var sourceFileName = "contract.pdf";
        var inboxPath = Path.Combine(_baseDir, "inbox", sourceFileName);
        var processingPath = Path.Combine(_baseDir, "processing", sourceFileName);
        await File.WriteAllTextAsync(inboxPath, "original");
        await File.WriteAllTextAsync(processingPath, "processing copy");

        var workspace = Path.Combine(_baseDir, "workspace");
        Directory.CreateDirectory(workspace);
        var repo = new Repository(workspace, new BranchName("legal-analysis"), string.Empty);

        var changes = new List<CodeChange>
        {
            new(new FilePath("analysis.md"), "# Risk Assessment\nHigh risk clause found.", "create"),
        };

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)changes);
        pipeline.Set(ContextKeys.SourceFilePath, inboxPath);

        var config = new SourceConfig { Type = "LocalFolder", Path = _baseDir };
        var context = new DeliverOutputContext(config, repo, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var outboxFiles = Directory.GetFiles(Path.Combine(_baseDir, "outbox"));
        outboxFiles.Should().HaveCount(1);
        outboxFiles[0].Should().Contain("contract-analysis.md");

        var archiveFiles = Directory.GetFiles(Path.Combine(_baseDir, "archive"));
        archiveFiles.Should().HaveCount(1);

        File.Exists(processingPath).Should().BeFalse("processing file should be moved to archive");
        File.Exists(inboxPath).Should().BeFalse("inbox file should be deleted");
    }

    [Fact]
    public async Task ExecuteAsync_NoCodeChanges_ReturnsFail()
    {
        var workspace = Path.Combine(_baseDir, "workspace");
        Directory.CreateDirectory(workspace);
        var repo = new Repository(workspace, new BranchName("legal-analysis"), string.Empty);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SourceFilePath, "/some/path.pdf");

        var context = new DeliverOutputContext(new SourceConfig { Path = _baseDir }, repo, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No compiled analysis");
    }
}
