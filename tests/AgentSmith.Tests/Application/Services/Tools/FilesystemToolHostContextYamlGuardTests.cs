using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0193: write_file / edit / multi_edit refuse .agentsmith/contexts/*/context.yaml.
/// Belt-and-suspenders so a stale skill or a confused agent can't bypass the
/// typed write path.
/// </summary>
public sealed class FilesystemToolHostContextYamlGuardTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();

    public FilesystemToolHostContextYamlGuardTests()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
    }

    [Theory]
    [InlineData(".agentsmith/contexts/default/context.yaml")]
    [InlineData(".agentsmith/contexts/api/context.yaml")]
    [InlineData("repo/.agentsmith/contexts/server/context.yaml")]
    [InlineData(".agentsmith/contexts/anything/context.YAML")]
    public async Task WriteFile_RejectsContextYamlPath_WithHintToWriteContextYaml(string path)
    {
        var sut = NewHost();

        var result = await sut.WriteFile(path, "meta:\n  workdir: '.'\n", CancellationToken.None);

        result.Should().StartWith("Error:");
        result.Should().Contain("write_context_yaml");
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WriteFile_NonContextYamlPath_ProceedsNormally()
    {
        var sut = NewHost();

        var result = await sut.WriteFile("src/Foo.cs", "// hello", CancellationToken.None);

        result.Should().StartWith("File written");
    }

    [Fact]
    public async Task Edit_RejectsContextYamlPath()
    {
        var sut = NewHost();

        var result = await sut.Edit(
            path: ".agentsmith/contexts/default/context.yaml",
            old_string: "meta:",
            new_string: "meta: {}",
            replace_all: false,
            CancellationToken.None);

        result.Should().Contain("write_context_yaml");
    }

    [Fact]
    public async Task MultiEdit_RejectsContextYamlPath()
    {
        var sut = NewHost();

        var result = await sut.MultiEdit(
            path: ".agentsmith/contexts/default/context.yaml",
            edits: new[] { new FilesystemToolHost.MultiEditOp("a", "b", false) },
            dry_run: false,
            CancellationToken.None);

        result.Should().Contain("write_context_yaml");
    }

    private FilesystemToolHost NewHost() => new(
        _sandboxMock.Object, repoPath: "/work", logger: NullLogger<FilesystemToolHost>.Instance);
}
