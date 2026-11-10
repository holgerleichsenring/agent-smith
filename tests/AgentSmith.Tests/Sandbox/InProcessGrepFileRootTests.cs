using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-10-fc66: grep_in_tree takes one file as its root, so the in-process sandbox searches that file the way the
/// sandbox agent already did — the design turn's only search tool works on every backend.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InProcessGrepFileRootTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("grep-file-root").FullName;

    [Fact]
    public async Task InProcessSandbox_GrepOnAFileRoot_SearchesThatFile()
    {
        Directory.CreateDirectory(Path.Combine(_work, "src"));
        await File.WriteAllLinesAsync(Path.Combine(_work, "src", "a.cs"), ["class A", "  void Notify()", "}"]);
        await File.WriteAllLinesAsync(Path.Combine(_work, "src", "b.cs"), ["void Notify() in b"]);
        await using var sandbox = new InProcessSandbox("t", _work, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);

        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Grep,
            Path: "src/a.cs", Pattern: "Notify"), progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0, result.ErrorMessage);
        result.OutputContent.Should().Contain("\"line\":2").And.Contain("a.cs").And.NotContain("b.cs");
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);
}
