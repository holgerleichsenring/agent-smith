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
    public async Task ExecuteAsync_TestCommandPresent_NoSandbox_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        // p0155: ci.test_command is the source of truth for what to run; the
        // PrimaryLanguage switch is gone.
        pipeline.Set(ContextKeys.ProjectMap, new ProjectMap(
            PrimaryLanguage: "csharp",
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<Module>(),
            TestProjects: Array.Empty<TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(HasCi: true, BuildCommand: null, TestCommand: "dotnet test", CiSystem: null)));

        var context = NewTestContext(pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("sandbox");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCiTestCommand_StillSkipsAsOk()
    {
        // Regression guard: empty ci.test_command is a legitimate "nothing to run"
        // signal (analyzer found no CI hint). Soft skip, never a hard fail.
        var context = NewTestContext(new PipelineContext());

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("ProjectMap missing");
    }

    private static TestContext NewTestContext(PipelineContext pipeline)
    {
        var repo = new Repository(
            currentBranch: new BranchName("main"),
            remoteUrl: "https://example.com/repo.git");
        return new TestContext(repo, new List<CodeChange>(), pipeline);
    }
}
