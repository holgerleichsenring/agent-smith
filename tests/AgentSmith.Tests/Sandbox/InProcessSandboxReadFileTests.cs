using AgentSmith.Application.Services.Specs;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InProcessSandboxReadFileTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "agentsmith-tests-" + Guid.NewGuid().ToString("N"));

    public InProcessSandboxReadFileTests() => Directory.CreateDirectory(_workDir);

    public void Dispose() { try { Directory.Delete(_workDir, recursive: true); } catch { } }

    [Fact]
    public async Task InProcessSandbox_ReadFile_NumberedFileEndingInNewline_ShowsExactlyItsLines()
    {
        (await ReadAsync("a\nb\n", withLineNumbers: true)).Should().Be("1\ta\n2\tb");
    }

    [Fact]
    public async Task InProcessSandbox_ReadFile_NumberedEmptyFile_ShowsNoLine()
    {
        (await ReadAsync("", withLineNumbers: true)).Should().BeEmpty();
    }

    [Fact]
    public async Task InProcessSandbox_ReadFile_NumberedFileWithoutTrailingNewline_IsUnchanged()
    {
        (await ReadAsync("a\nb", withLineNumbers: true)).Should().Be("1\ta\n2\tb");
    }

    [Fact]
    public async Task InProcessSandbox_ReadFile_UnnumberedWholeFile_KeepsTheTrailingNewline()
    {
        (await ReadAsync("a\nb\n", withLineNumbers: false)).Should().Be("a\nb\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a\n")]
    [InlineData("a\nb")]
    [InlineData("a\nb\n")]
    [InlineData("a\n\n")]
    public async Task InProcessSandbox_NumberedLineCount_EqualsEvidenceLineCount(string content)
    {
        var numbered = await ReadAsync(content, withLineNumbers: true);

        var shown = numbered.Length == 0 ? 0 : numbered.Split('\n').Length;
        shown.Should().Be(EvidenceLineCount.Of(content));
    }

    private async Task<string> ReadAsync(string content, bool withLineNumbers)
    {
        await File.WriteAllTextAsync(Path.Combine(_workDir, "fixture.txt"), content);
        await using var sandbox = new InProcessSandbox("read", _workDir, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            Path: "fixture.txt", WithLineNumbers: withLineNumbers), progress: null, CancellationToken.None);
        result.ExitCode.Should().Be(0);
        return result.OutputContent!;
    }
}
