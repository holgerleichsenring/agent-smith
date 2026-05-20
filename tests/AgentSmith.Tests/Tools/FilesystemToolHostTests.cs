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
    public void GetTools_PlanPhase_IncludesRunCommandForRecon()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Plan, null));

        // Plan / Verify / Investigate / Review / Discuss / Filter / Synthesize all share
        // the read+shell set so recon skills can run ls/find/curl freely at every stage.
        names.Should().BeEquivalentTo("read_file", "grep", "glob", "list_files", "run_command", "http_request");
    }

    [Fact]
    public void GetTools_ImplementationPhase_ReturnsAllTools()
    {
        var host = Build();

        var names = NamesOf(host.GetTools(SkillExecutionPhase.Implementation, null));

        names.Should().BeEquivalentTo("read_file", "write_file", "edit", "list_files", "grep", "glob", "run_command", "http_request");
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

        // Bootstrap and Implementation are the write-capable sets; both expose
        // edit (targeted string-replace) and write_file (full overwrite).
        names.Should().BeEquivalentTo("read_file", "grep", "glob", "list_files", "write_file", "edit", "run_command", "http_request");
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
    public async Task GetChanges_TwoWritesSamePath_DedupedToLatestContent()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.WriteFile),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = Build(sandbox.Object);

        await host.WriteFile("src/x.cs", "v1");
        await host.WriteFile("src/x.cs", "v2");

        var changes = host.GetChanges();
        changes.Should().ContainSingle();
        changes[0].Path.Value.Should().Be("src/x.cs");
        changes[0].Content.Should().Be("v2");
    }

    [Fact]
    public async Task GetChanges_WritesToTwoDifferentPaths_KeepsBoth()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.WriteFile),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null));
        var host = Build(sandbox.Object);

        await host.WriteFile("src/a.cs", "a");
        await host.WriteFile("src/b.cs", "b");

        host.GetChanges().Select(c => c.Path.Value).Should().BeEquivalentTo(new[] { "src/a.cs", "src/b.cs" });
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
