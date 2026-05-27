using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Sandbox;

/// <summary>
/// Regression-guard: <see cref="InProcessSandbox"/> DisposeAsync used to
/// <c>Directory.Delete(workDir, recursive: true)</c> unconditionally, which
/// destroyed operator-owned source trees when the sandbox was created via
/// <see cref="InProcessSandboxFactory"/> with a <see cref="SandboxSpec.InitialSourcePath"/>
/// (e.g. an <c>api-scan --source-path</c> CLI invocation against a real working
/// copy). This suite asserts the ownsWorkDir contract: the sandbox only removes
/// what it created, never what the operator handed in.
/// </summary>
public sealed class InProcessSandboxWorkDirOwnershipTests
{
    [Fact]
    public async Task DisposeAsync_OwnsWorkDirFalse_LeavesDirectoryAndContentsIntact()
    {
        var operatorPath = Path.Combine(Path.GetTempPath(), $"agentsmith-ownership-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operatorPath);
        var sentinel = Path.Combine(operatorPath, "important-work.txt");
        await File.WriteAllTextAsync(sentinel, "do not delete me");
        try
        {
            var sandbox = new InProcessSandbox(
                jobId: "ownership-test",
                workDir: operatorPath,
                ownsWorkDir: false,
                logger: NullLogger<InProcessSandbox>.Instance);

            await sandbox.DisposeAsync();

            Directory.Exists(operatorPath).Should().BeTrue(
                "operator-provided workDir must survive Dispose when ownsWorkDir=false");
            File.Exists(sentinel).Should().BeTrue(
                "files inside the operator-provided workDir must not be recursively deleted");
            (await File.ReadAllTextAsync(sentinel)).Should().Be("do not delete me");
        }
        finally
        {
            if (Directory.Exists(operatorPath))
                Directory.Delete(operatorPath, recursive: true);
        }
    }

    [Fact]
    public async Task DisposeAsync_OwnsWorkDirTrue_RemovesOwnTempDirectory()
    {
        var sandboxTemp = Path.Combine(Path.GetTempPath(), $"agentsmith-ownership-own-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandboxTemp);

        var sandbox = new InProcessSandbox(
            jobId: "ownership-test",
            workDir: sandboxTemp,
            ownsWorkDir: true,
            logger: NullLogger<InProcessSandbox>.Instance);

        await sandbox.DisposeAsync();

        Directory.Exists(sandboxTemp).Should().BeFalse(
            "sandbox-owned tempdir must be cleaned up on Dispose");
    }

    [Fact]
    public async Task Factory_ReusesInitialSourcePath_ProducesNonOwningSandbox()
    {
        var operatorPath = Path.Combine(Path.GetTempPath(), $"agentsmith-factory-ownership-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operatorPath);
        var sentinel = Path.Combine(operatorPath, "marker.txt");
        await File.WriteAllTextAsync(sentinel, "operator file");
        try
        {
            var factory = new InProcessSandboxFactory(NullLoggerFactory.Instance);
            var spec = new SandboxSpec(
                ToolchainImage: "irrelevant",
                Resources: ResourceLimits.Default,
                InitialSourcePath: operatorPath);

            var sandbox = await factory.CreateAsync(spec, CancellationToken.None);
            await sandbox.DisposeAsync();

            Directory.Exists(operatorPath).Should().BeTrue(
                "factory must produce a non-owning sandbox when reusing InitialSourcePath");
            File.Exists(sentinel).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(operatorPath))
                Directory.Delete(operatorPath, recursive: true);
        }
    }

    [Fact]
    public async Task Factory_FallbackTempDir_ProducesOwningSandbox()
    {
        var factory = new InProcessSandboxFactory(NullLoggerFactory.Instance);
        var spec = new SandboxSpec(
            ToolchainImage: "irrelevant",
            Resources: ResourceLimits.Default);

        var sandbox = await factory.CreateAsync(spec, CancellationToken.None);
        var workDir = ((InProcessSandbox)sandbox).WorkDir;
        Directory.Exists(workDir).Should().BeTrue();

        await sandbox.DisposeAsync();

        Directory.Exists(workDir).Should().BeFalse(
            "factory-allocated fallback tempdir must be cleaned up on Dispose");
    }
}
