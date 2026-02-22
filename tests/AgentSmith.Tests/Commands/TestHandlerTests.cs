using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class TestHandlerTests
{
    private readonly TestHandler _handler = new(
        NullLoggerFactory.Instance.CreateLogger<TestHandler>());

    [Fact]
    public async Task ExecuteAsync_NoTestFrameworkDetected_SkipsTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = new Repository(tempDir, new BranchName("main"), "https://github.com/org/repo.git");
            var pipeline = new PipelineContext();
            var context = new TestContext(repo, new List<CodeChange>(), pipeline);

            var result = await _handler.ExecuteAsync(context);

            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Contain("No test framework detected");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
