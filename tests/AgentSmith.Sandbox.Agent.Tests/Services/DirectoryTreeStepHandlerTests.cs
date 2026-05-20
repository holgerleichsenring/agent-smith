using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public sealed class DirectoryTreeStepHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public DirectoryTreeStepHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-tree-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() { try { Directory.Delete(_tempDir, recursive: true); } catch { } }

    [Fact]
    public async Task DirectoryTree_RendersNestedStructure()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "a.cs"), "");
        File.WriteAllText(Path.Combine(_tempDir, "README.md"), "");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            Path: _tempDir);

        var result = await NewHandler().HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().Contain("README.md");
        result.OutputContent.Should().Contain("src/");
        result.OutputContent.Should().Contain("a.cs");
    }

    [Fact]
    public async Task DirectoryTree_HonoursMaxDepth()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "level1", "level2", "level3"));
        File.WriteAllText(Path.Combine(_tempDir, "level1", "level2", "level3", "deep.txt"), "");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            Path: _tempDir, MaxDepth: 2);

        var result = await NewHandler().HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.OutputContent.Should().Contain("level1/");
        result.OutputContent.Should().Contain("level2/");
        result.OutputContent.Should().NotContain("deep.txt");
    }

    [Fact]
    public async Task DirectoryTree_HonoursExcludeGlobs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "keep.cs"), "");
        File.WriteAllText(Path.Combine(_tempDir, "skip.min.js"), "");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            Path: _tempDir, ExcludeGlobs: new[] { "*.min.js" });

        var result = await NewHandler().HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.OutputContent.Should().Contain("keep.cs");
        result.OutputContent.Should().NotContain("skip.min.js");
    }

    [Fact]
    public async Task DirectoryTree_AlwaysExcludesNoisyDirs()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "bin"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "real_code"));
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            Path: _tempDir);

        var result = await NewHandler().HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.OutputContent.Should().Contain("real_code");
        result.OutputContent.Should().NotContain("node_modules");
        result.OutputContent.Should().NotContain("bin");
    }

    [Fact]
    public async Task DirectoryTree_MissingDirectory_ReturnsFailure()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.DirectoryTree,
            Path: Path.Combine(_tempDir, "does-not-exist"));

        var result = await NewHandler().HandleAsync(step, _ => Task.CompletedTask, CancellationToken.None);

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("not found");
    }

    private static DirectoryTreeStepHandler NewHandler() => new(NullLogger<DirectoryTreeStepHandler>.Instance);
}
