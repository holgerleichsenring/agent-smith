using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// Regression: when the sandbox factory failed to create a sandbox in production,
/// TestHandler returned Ok with "Test execution skipped — no sandbox available",
/// letting the pipeline produce a commit + PR without ever validating the changes.
/// The fix surfaces the missing sandbox as a pipeline failure so CommitAndPR is
/// short-circuited and the operator sees the broken sandbox path.
/// </summary>
public sealed class TestHandlerNoSandboxFailsTests
{
    private readonly TestHandler _handler = new(
        new TrxResultParser(),
        NullLoggerFactory.Instance.CreateLogger<TestHandler>());

    [Fact]
    public async Task ExecuteAsync_TestFrameworkDetected_NoSandbox_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DetectedProject, new DetectedProject(
            Language: "csharp",
            Runtime: ".NET 8",
            PackageManager: null,
            BuildCommand: "dotnet build",
            TestCommand: "dotnet test",
            Frameworks: Array.Empty<string>(),
            Infrastructure: Array.Empty<string>(),
            KeyFiles: Array.Empty<string>(),
            Sdks: Array.Empty<string>()));

        var context = NewTestContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("sandbox");
    }

    [Fact]
    public async Task ExecuteAsync_NoTestFrameworkDetected_StillSkipsAsOk()
    {
        // Regression guard: "no test framework" is a property of the project (legitimately
        // nothing to run), not a runtime defect. That path remains a soft skip.
        var context = NewTestContext(new PipelineContext());

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No test framework detected");
    }

    private static TestContext NewTestContext(PipelineContext pipeline)
    {
        var repo = new Repository(
            currentBranch: new BranchName("main"),
            remoteUrl: "https://example.com/repo.git");
        return new TestContext(repo, new List<CodeChange>(), pipeline);
    }
}
