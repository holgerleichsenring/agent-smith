using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class SandboxGlobalConfigDefaultsTests
{
    [Fact]
    public void SandboxGlobalConfig_StepTimeoutSeconds_DefaultsTo900()
    {
        var config = new SandboxGlobalConfig();

        // 900s (15 min) is the operator-validated minimum that lets a
        // real-world C# / Node test suite finish restore + build + run
        // inside a clean DockerSandbox. 120 (p0200 v1) and 300 (legacy
        // TestHandler default) both wedged Sample on 2026-06-02.
        config.StepTimeoutSeconds.Should().Be(900);
    }

    [Fact]
    public void OrchestratorGlobalConfig_MaxRunWallTimeSeconds_DefaultsTo1800()
    {
        var config = new OrchestratorGlobalConfig();

        config.MaxRunWallTimeSeconds.Should().Be(1800);
    }
}
