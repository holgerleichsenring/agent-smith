using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class SandboxGlobalConfigDefaultsTests
{
    [Fact]
    public void SandboxGlobalConfig_StepTimeoutSeconds_DefaultsTo120()
    {
        var config = new SandboxGlobalConfig();

        config.StepTimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void OrchestratorGlobalConfig_MaxRunWallTimeSeconds_DefaultsTo1800()
    {
        var config = new OrchestratorGlobalConfig();

        config.MaxRunWallTimeSeconds.Should().Be(1800);
    }
}
