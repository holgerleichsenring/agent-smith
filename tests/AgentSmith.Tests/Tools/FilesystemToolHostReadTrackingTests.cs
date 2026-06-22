using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0279: FilesystemToolHost records the distinct paths it successfully read, for the
/// scan findings-anchor + the coverage re-drive gauge.
/// </summary>
public sealed class FilesystemToolHostReadTrackingTests
{
    private static FilesystemToolHost HostReturning(string output)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, output));
        return new FilesystemToolHost(sandbox.Object);
    }

    [Fact]
    public async Task FilesystemToolHost_TracksDistinctReadPaths_ExposesReadSet()
    {
        var host = HostReturning("line content");

        await host.ReadFile("src/A.cs");
        await host.ReadFile("src/B.cs");
        await host.ReadFile("src/A.cs"); // duplicate

        host.ReadPaths.Should().BeEquivalentTo(["src/A.cs", "src/B.cs"]);
    }

    [Fact]
    public async Task FilesystemToolHost_FailedRead_NotTracked()
    {
        var host = HostReturning("Error: file not found");

        await host.ReadFile("src/missing.cs");

        host.ReadPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task FilesystemToolHost_ConcurrentReadsAndWrites_ThreadSafe()
    {
        // p0280: the master shares ONE fs across concurrent sub-agents — read-set + change
        // tracking must not race. Hammer 100 distinct concurrent reads + writes.
        var host = HostReturning("content");
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            await host.ReadFile($"src/read{i}.cs");
            await host.WriteFile($"src/write{i}.cs", "x");
        });

        await Task.WhenAll(tasks); // must not throw (no unguarded HashSet/List mutation)

        host.ReadPaths.Should().HaveCount(100);
        host.GetChanges().Should().HaveCount(100);
    }
}
