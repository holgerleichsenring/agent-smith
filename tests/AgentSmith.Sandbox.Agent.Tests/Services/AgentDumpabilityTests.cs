using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>2026-11-25-9992d: the hardening never stops the agent — off Linux it does nothing, on Linux a libc that
/// cannot be reached is a warning — and on Linux the process ends up non-dumpable.</summary>
public sealed class AgentDumpabilityTests
{
    [Fact]
    public void Disable_NeverThrows()
    {
        var act = () => new AgentDumpability(NullLogger<AgentDumpability>.Instance).Disable();

        act.Should().NotThrow();
    }

    [Fact]
    public void Disable_OnLinux_MakesTheProcessNonDumpable()
    {
        if (!OperatingSystem.IsLinux()) return; // only Linux has PR_SET_DUMPABLE and /proc/self/status

        new AgentDumpability(NullLogger<AgentDumpability>.Instance).Disable();

        File.ReadAllLines("/proc/self/status").Should().Contain("Dumpable:\t0");
    }
}
