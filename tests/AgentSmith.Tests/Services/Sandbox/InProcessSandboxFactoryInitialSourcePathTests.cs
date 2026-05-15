using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Sandbox;

/// <summary>
/// Regression-guard for the CLI api-security-scan source→sandbox handoff.
/// TryCheckoutSourceHandler clones the source host-side under
/// /var/folders/.../agentsmith-src-*. Pre-fix, InProcessSandboxFactory ignored
/// this and created an empty new temp dir as /work — BootstrapCheckHandler
/// then reported `context.yaml=False, principles=False` even though the
/// cloned source contained the bootstrap files.
/// </summary>
public sealed class InProcessSandboxFactoryInitialSourcePathTests
{
    private static readonly ResourceLimits Resources = ResourceLimits.Default;

    [Fact]
    public async Task CreateAsync_InitialSourcePathSet_SandboxWorkDirIsTheClonedSource()
    {
        var clonedSource = Path.Combine(Path.GetTempPath(), $"agentsmith-src-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(clonedSource);
        await File.WriteAllTextAsync(
            Path.Combine(clonedSource, "marker.txt"),
            "from-cloned-source");
        try
        {
            var factory = new InProcessSandboxFactory(NullLoggerFactory.Instance);
            var spec = new SandboxSpec(
                ToolchainImage: "irrelevant",
                Resources: Resources,
                InitialSourcePath: clonedSource);

            await using var sandbox = await factory.CreateAsync(spec, CancellationToken.None);

            // Copy marker.txt to a side file inside /work — if /work maps to clonedSource,
            // the side file lands inside clonedSource and we can read it from the host.
            var step = new Step(
                SchemaVersion: Step.CurrentSchemaVersion,
                StepId: Guid.NewGuid(),
                Kind: StepKind.Run,
                Command: "/bin/sh",
                Args: ["-c", "cp marker.txt copy.txt"],
                WorkingDirectory: "/work");

            var result = await sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

            result.ExitCode.Should().Be(0);
            var copy = Path.Combine(clonedSource, "copy.txt");
            File.Exists(copy).Should().BeTrue(
                "sandbox /work must map to the cloned source dir, not a fresh empty temp dir");
            (await File.ReadAllTextAsync(copy)).Trim().Should().Be("from-cloned-source");
        }
        finally
        {
            if (Directory.Exists(clonedSource))
                Directory.Delete(clonedSource, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_InitialSourcePathNull_FallsBackToFreshTempDir()
    {
        var factory = new InProcessSandboxFactory(NullLoggerFactory.Instance);
        var spec = new SandboxSpec(
            ToolchainImage: "irrelevant",
            Resources: Resources);

        await using var sandbox = await factory.CreateAsync(spec, CancellationToken.None);

        // The sandbox MUST be functional with an auto-created empty workDir.
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "touch sentinel.txt"],
            WorkingDirectory: "/work");

        var result = await sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0,
            "fallback workDir must be a writable empty temp dir when no InitialSourcePath provided");
    }

    [Fact]
    public async Task CreateAsync_InitialSourcePathPointsAtMissingDir_FallsBackToFreshTempDir()
    {
        var bogus = Path.Combine(Path.GetTempPath(), $"agentsmith-src-bogus-{Guid.NewGuid():N}");
        // Note: NOT created — InitialSourcePath references a non-existent path.

        var factory = new InProcessSandboxFactory(NullLoggerFactory.Instance);
        var spec = new SandboxSpec(
            ToolchainImage: "irrelevant",
            Resources: Resources,
            InitialSourcePath: bogus);

        await using var sandbox = await factory.CreateAsync(spec, CancellationToken.None);

        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", "touch sentinel.txt"],
            WorkingDirectory: "/work");

        var result = await sandbox.RunStepAsync(step, progress: null, CancellationToken.None);

        result.ExitCode.Should().Be(0,
            "missing InitialSourcePath must fall back to a fresh temp dir, not crash");
    }
}
