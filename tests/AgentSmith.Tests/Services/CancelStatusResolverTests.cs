using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0259: a cancel terminates as its own status, not the success/failed binary.
// operator + watchdog are intentional stops → "cancelled"; a vanished sandbox is
// a crash dressed as a cancel → stays "failed" so the OOM cause is not hidden.
public sealed class CancelStatusResolverTests
{
    [Theory]
    [InlineData("operator")]
    [InlineData("watchdog-wall-time")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("some-future-reason")]
    public void ResolveCancelStatus_IntentionalCancel_Cancelled(string? reason)
    {
        ExecutePipelineUseCase.ResolveCancelStatus(reason).Should().Be("cancelled");
    }

    [Fact]
    public void ResolveCancelStatus_VanishedSandbox_StaysFailed()
    {
        ExecutePipelineUseCase.ResolveCancelStatus("sandbox-vanished").Should().Be("failed");
    }
}
