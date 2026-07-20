using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Server.Services.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight;

// p0355: the startup WARN that stops the server OOMKilling silently on an
// under-provisioned pod memory limit (the upstream cause of reaped runs,
// truncated event trails, and orphaned sandbox pods).
public sealed class ServerMemoryFloorCheckTests
{
    private const long Mi = 1024 * 1024;

    [Fact]
    public async Task RunAsync_CeilingBelowFloor_FailsWithSizingHint()
    {
        var check = new ServerMemoryFloorCheck(() => 256 * Mi);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("256Mi");
        result.FixHint.Should().Contain("512Mi");
    }

    [Fact]
    public async Task RunAsync_CeilingAtOrAboveFloor_Passes()
    {
        var check = new ServerMemoryFloorCheck(() => 1024 * Mi);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("1024Mi");
    }
}
