using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Application.Commands.Handlers;
using AgentSmith.Contracts.Commands;
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
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "# Test Principles");

        var pipeline = new PipelineContext();
        var context = new LoadCodingPrinciplesContext(tempFile, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.Success.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.CodingPrinciples).Should().Be("# Test Principles");

        File.Delete(tempFile);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var context = new LoadCodingPrinciplesContext("/nonexistent/path.md", pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
