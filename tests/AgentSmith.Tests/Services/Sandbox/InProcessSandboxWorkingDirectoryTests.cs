using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Sandbox;

/// <summary>
/// Regression: handlers build Steps with WorkingDirectory =
/// Repository.SandboxWorkPath (literal "/work"). Pre-fix
/// InProcessSandbox.BuildStartInfo passed that string verbatim to
/// Process.Start, which fails on dev hosts (macOS / Windows / any host
/// without /work) with "No such file or directory". The fix routes
/// the WorkingDirectory through ResolvePath so /work is translated to
/// the sandbox's actual temp dir.
/// </summary>
public sealed class InProcessSandboxWorkingDirectoryTests : IAsyncDisposable
{
    private readonly string _workDir;
    private readonly InProcessSandbox _sandbox;

    public InProcessSandboxWorkingDirectoryTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"agentsmith-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _sandbox = new InProcessSandbox(
            jobId: "test", workDir: _workDir, ownsWorkDir: true,
            logger: NullLogger<InProcessSandbox>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _sandbox.DisposeAsync();
    }

    [Fact]
    public async Task RunStepAsync_WorkingDirectoryWork_TranslatesToWorkDir()
    {
        // pwd-equivalent: write the cwd to a sentinel file. We assert the
        // file ends up in the sandbox's temp dir, proving WorkingDirectory
        // was translated from "/work" to the actual temp path.
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "pwd > pwd.txt"],
            WorkingDirectory: "/work");

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        var pwdFile = Path.Combine(_workDir, "pwd.txt");
        File.Exists(pwdFile).Should().BeTrue("pwd.txt must land in the temp workDir, not /work");
        var pwd = (await File.ReadAllTextAsync(pwdFile)).Trim();
        // macOS resolves /var/folders/... to /private/var/folders/...; basename
        // comparison is enough — what we're checking is that "/work" was NOT
        // passed through verbatim.
        Path.GetFileName(pwd).Should().Be(Path.GetFileName(_workDir));
        pwd.Should().NotBe("/work");
    }

    [Fact]
    public async Task RunStepAsync_WorkingDirectoryWorkSubpath_TranslatesUnderWorkDir()
    {
        var sub = Path.Combine(_workDir, "subdir");
        Directory.CreateDirectory(sub);

        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "pwd > marker.txt"],
            WorkingDirectory: "/work/subdir");

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(sub, "marker.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task RunStepAsync_WorkingDirectoryNull_DefaultsToWorkDir()
    {
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "pwd > pwd.txt"],
            WorkingDirectory: null);

        var result = await _sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(_workDir, "pwd.txt")).Should().BeTrue();
    }
}
