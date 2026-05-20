using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Sandbox;

public sealed class SandboxFileReaderListShapeTests
{
    [Fact]
    public async Task ListAsync_ObjectShape_ExtractsPathField()
    {
        var json = """[{"path":"a.cs","size_bytes":10,"mtime":"2026-05-20T22:00:00Z","is_directory":false},{"path":"b/","is_directory":true}]""";
        var reader = BuildReader(json);

        var paths = await reader.ListAsync("/work", maxDepth: 1, CancellationToken.None);

        paths.Should().Equal("a.cs", "b/");
    }

    [Fact]
    public async Task ListAsync_LegacyStringShape_StillParses()
    {
        var json = """["a.cs","b/"]""";
        var reader = BuildReader(json);

        var paths = await reader.ListAsync("/work", maxDepth: 1, CancellationToken.None);

        paths.Should().Equal("a.cs", "b/");
    }

    [Fact]
    public async Task ListAsync_EmptyArray_ReturnsEmpty()
    {
        var reader = BuildReader("[]");

        var paths = await reader.ListAsync("/work", maxDepth: 1, CancellationToken.None);

        paths.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_NonZeroExitCode_ReturnsEmpty()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 1, false, 0.1, "fail", null));
        var reader = new SandboxFileReader(sandbox.Object);

        var paths = await reader.ListAsync("/work", maxDepth: 1, CancellationToken.None);

        paths.Should().BeEmpty();
    }

    private static SandboxFileReader BuildReader(string outputJson)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Kind == StepKind.ListFiles),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, outputJson));
        return new SandboxFileReader(sandbox.Object);
    }
}
