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

    // p0152: split the overloaded grep / glob / list_files surface into explicit
    // primitives — grep_in_file vs grep_in_tree (file-or-directory disambiguated),
    // find_files (was glob), list_directory (was list_files). Old names remain in
    // every phase set as deprecated aliases until the agent-smith-skills catalog
    // migrates its prompts; the assertions below cover both new + deprecated.
    // p0153: surface gains directory_tree + multi_edit; deprecated grep / glob /
    // list_files stay registered as forwarders until p0154 ships the skills rename.
    private static readonly string[] ReadOnlyToolNames =
    {
        "read_file", "grep_in_file", "grep_in_tree", "find_files", "list_directory",
        "directory_tree", "run_command", "http_request",
        "grep", "glob", "list_files" // deprecated aliases
    };

    private static readonly string[] WriteCapableToolNames =
    {
        "read_file", "write_file", "edit", "multi_edit",
        "grep_in_file", "grep_in_tree", "find_files",
        "list_directory", "directory_tree", "run_command", "http_request",
        "grep", "glob", "list_files" // deprecated aliases
    };

    [Fact]
    public void GetTools_PlanPhase_IncludesRunCommandForRecon()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Plan, null));

        names.Should().BeEquivalentTo(ReadOnlyToolNames);
    }

    [Fact]
    public void GetTools_ImplementationPhase_ReturnsAllTools()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Implementation, null));

        names.Should().BeEquivalentTo(WriteCapableToolNames);
    }

    [Fact]
    public void GetTools_VerifyPhase_IncludesRunCommandExcludesWriteFile()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Verify, null));

        names.Should().Contain("run_command").And.NotContain("write_file");
    }

    [Fact]
    public void GetTools_BootstrapPhase_IncludesWriteCapableSet()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Bootstrap, null));

        names.Should().BeEquivalentTo(WriteCapableToolNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SkillExecutionPhase.Review)]
    [InlineData(SkillExecutionPhase.Discuss)]
    [InlineData(SkillExecutionPhase.Filter)]
    [InlineData(SkillExecutionPhase.Synthesize)]
    public void GetTools_EveryReadPhase_ExposesRunCommand(SkillExecutionPhase? phase)
    {
        var host = Build();

        var names = NamesOf(host.GetTools(phase, null));

        names.Should().Contain("run_command");
    }

    [Fact]
    public async Task RunCommand_BlocksDestructiveCommand_ReturnsErrorWithoutInvokingSandbox()
    {
        var sandbox = new Mock<ISandbox>(MockBehavior.Strict);
        var host = Build(sandbox.Object);

        var result = await host.RunCommand("rm -rf /");

        result.Should().Contain("blocked").And.Contain("destructive");
        sandbox.Verify(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Never);
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

        result.Should().Contain("exit_code: 0").And.Contain("stdout:").And.Contain("stderr:");
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
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null,
                "[{\"path\":\"x.cs\",\"line\":1,\"text\":\"foo\",\"kind\":\"match\"}]"));
        var host = Build(sandbox.Object);

        var result = await host.Grep("foo", ".");

        result.Should().Contain("x.cs");
        sandbox.VerifyAll();
    }
}
