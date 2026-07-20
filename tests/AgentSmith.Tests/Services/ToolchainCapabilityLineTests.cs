using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0356: the environment probe's distiller — raw tool version blurbs become the
// compact capability line the master context carries.
public sealed class ToolchainCapabilityLineTests
{
    [Fact]
    public void Distill_TypicalProbeOutput_BuildsCapabilityLine()
    {
        var stdout = string.Join("\n",
            "bash GNU bash, version 5.2.21(1)-release (aarch64-unknown-linux-gnu)",
            "git git version 2.43.0",
            "dotnet 8.0.408",
            "node v20.11.1",
            "go go version go1.22.1 linux/arm64");

        var line = ToolchainCapabilityLine.Distill(stdout);

        line.Should().Be("bash 5.2.21, git 2.43.0, dotnet 8.0.408, node 20.11.1, go 1.22.1");
    }

    [Fact]
    public void Distill_ToolWithoutParseableVersion_ReportsBareToolName()
    {
        ToolchainCapabilityLine.Distill("make some unversioned banner")
            .Should().Be("make", "presence IS the capability even when no version parses");
    }

    [Fact]
    public void Distill_EmptyOutput_Null()
    {
        ToolchainCapabilityLine.Distill(null).Should().BeNull();
        ToolchainCapabilityLine.Distill("  \n ").Should().BeNull();
    }

    [Fact]
    public void ExtractStdout_LabeledRunCommandResult_ReturnsStdoutBlockOnly()
    {
        var output = "exit_code: 0\nelapsed_ms: 42\ntruncated: false\n\n"
            + "stdout:\ngit git version 2.43.0\ndotnet 8.0.408\n\nstderr:\nsome noise";

        ToolchainCapabilityLine.ExtractStdout(output)
            .Should().Be("git git version 2.43.0\ndotnet 8.0.408");
    }

    [Fact]
    public void ExtractStdout_NoStdoutSection_Null()
    {
        ToolchainCapabilityLine.ExtractStdout("Error: something").Should().BeNull();
    }
}
