using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class FilesystemToolHostTests
{
    private static FilesystemToolHost Build(
        ISandbox? sandbox = null,
        IPathReadGuard? readGuard = null,
        IPathWriteGuard? writeGuard = null)
        => new(sandbox ?? new Mock<ISandbox>().Object,
               repoPath: "/work",
               readGuard: readGuard,
               writeGuard: writeGuard);

    private static IReadOnlyList<string> NamesOf(IEnumerable<AIFunction> tools)
        => tools.Select(f => f.Name).ToList();

    [Fact]
    public void GetTools_PlanPhase_ReturnsReadOnlySet()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Plan, null));

        names.Should().BeEquivalentTo("ReadFile", "Grep", "ListFiles");
    }

    [Fact]
    public void GetTools_ImplementationPhase_ReturnsAllFiveTools()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Implementation, null));

        names.Should().BeEquivalentTo("ReadFile", "WriteFile", "ListFiles", "Grep", "RunCommand");
    }

    [Fact]
    public void GetTools_VerifyPhase_IncludesRunCommandExcludesWriteFile()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Verify, null));

        names.Should().Contain("RunCommand").And.NotContain("WriteFile");
    }

    [Fact]
    public void GetTools_BootstrapPhase_IncludesWriteFileExcludesRunCommand()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Bootstrap, null));

        names.Should().Contain("WriteFile").And.NotContain("RunCommand");
    }

    [Fact]
    public async Task ReadFile_PathGuardRejects_ReturnsStructuredError()
    {
        var readGuard = new Mock<IPathReadGuard>();
        readGuard.Setup(g => g.AssertReadable(It.IsAny<string>()))
            .Returns(Result.Fail(new GuardError
            {
                Kind = GuardErrorKind.OutsideRepo,
                Path = "../escape",
                Message = "Path outside repository"
            }));
        var host = Build(readGuard: readGuard.Object);

        var result = await host.ReadFile("../escape");

        result.Should().StartWith("Error:").And.Contain("Path outside repository");
    }

    [Fact]
    public async Task WriteFile_PathGuardRejects_ReturnsStructuredError()
    {
        var writeGuard = new Mock<IPathWriteGuard>();
        writeGuard.Setup(g => g.AssertWritable(It.IsAny<string>()))
            .Returns(Result.Fail(new GuardError
            {
                Kind = GuardErrorKind.WriteForbiddenInPhase,
                Path = "src/x.cs",
                Message = "Writes not allowed in Plan phase"
            }));
        var host = Build(writeGuard: writeGuard.Object);

        var result = await host.WriteFile("src/x.cs", "content");

        result.Should().StartWith("Error:").And.Contain("Writes not allowed");
    }

    [Fact]
    public async Task RunCommand_DelegatesToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.Run),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = Build(sandbox.Object);

        var result = await host.RunCommand("echo hi");

        result.Should().Contain("Exit code: 0");
        sandbox.VerifyAll();
    }

    [Fact]
    public async Task ListFiles_DelegatesToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.ListFiles),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[\"a.cs\",\"b.cs\"]"));
        var host = Build(sandbox.Object);

        var result = await host.ListFiles(".");

        result.Should().Contain("a.cs").And.Contain("b.cs");
        sandbox.VerifyAll();
    }

    [Fact]
    public async Task Grep_DelegatesToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.Grep),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "[{\"file\":\"x.cs\"}]"));
        var host = Build(sandbox.Object);

        var result = await host.Grep("foo", ".");

        result.Should().Contain("x.cs");
        sandbox.VerifyAll();
    }
}
