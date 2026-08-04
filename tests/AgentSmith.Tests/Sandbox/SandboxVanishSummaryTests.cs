using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0396: the vanish summary derives from the container's exit EVIDENCE
/// (oomKilled/exitCode) instead of the old blanket OOM guess — which sent an
/// operator tuning memory limits for a Redis-client-timeout agent crash.
/// </summary>
public sealed class SandboxVanishSummaryTests
{
    [Fact]
    public void VanishSummary_OomKilled_MemoryGuidance()
    {
        var summary = SandboxVanishSummary.Describe(exitCode: 137, oomKilled: true);

        summary.Should().Contain("OOM-killed")
            .And.Contain("memory limit")
            .And.Contain("137");
    }

    [Fact]
    public void VanishSummary_ExitCode3_NamesAgentCrash_NotOom()
    {
        var summary = SandboxVanishSummary.Describe(exitCode: 3, oomKilled: false);

        summary.Should().Contain("crashed")
            .And.Contain("exit code 3")
            .And.NotContain("memory limit", "the OOM guidance appears only when oomKilled is true");
    }

    [Fact]
    public void VanishSummary_CleanExitZero_NamedAsSuch()
    {
        var summary = SandboxVanishSummary.Describe(exitCode: 0, oomKilled: false);

        summary.Should().Contain("clean exit")
            .And.Contain("exit code 0")
            .And.NotContain("memory limit");
    }

    [Fact]
    public void VanishSummary_OtherExitCode_NamesTheCode()
    {
        var summary = SandboxVanishSummary.Describe(exitCode: 139, oomKilled: false);

        summary.Should().Contain("exit code 139")
            .And.NotContain("memory limit");
    }
}
