using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public class LoadCodingPrinciplesHandlerTests
{
    private readonly LoadCodingPrinciplesHandler _handler = new(
        NullLoggerFactory.Instance.CreateLogger<LoadCodingPrinciplesHandler>());

    [Fact]
    public async Task ExecuteAsync_FileExists_LoadsContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var relativePath = ".agentsmith/coding-principles.md";
        var fullPath = Path.Combine(tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "# Test Principles");

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadCodingPrinciplesContext(relativePath, repo, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.CodingPrinciples).Should().Be("# Test Principles");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsFail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadCodingPrinciplesContext("nonexistent/path.md", repo, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not found");

        Directory.Delete(tempDir, true);
    }
}
