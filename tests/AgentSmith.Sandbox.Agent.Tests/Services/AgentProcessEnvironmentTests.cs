using System.Diagnostics;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-10-02-35b2: a step's process does not inherit the agent's step-bus address or job id; a
/// step that sets one itself keeps its own value.
/// </summary>
public class AgentProcessEnvironmentTests
{
    [Fact]
    public void AgentProcessEnvironment_Scrub_RemovesTheAgentsRedisUrlAndJobId()
    {
        var info = new ProcessStartInfo("sh");
        info.Environment["REDIS_URL"] = "redis://bus:6379";
        info.Environment["JOB_ID"] = "job-1";
        info.Environment["HOME"] = "/root";

        AgentProcessEnvironment.Scrub(info, null);

        info.Environment.Keys.Should().NotContain(["REDIS_URL", "JOB_ID"]).And.Contain("HOME");
    }

    [Fact]
    public void AgentProcessEnvironment_Scrub_KeepsAStepsOwnValue()
    {
        var info = new ProcessStartInfo("sh");
        info.Environment["REDIS_URL"] = "redis://tests:6379";

        AgentProcessEnvironment.Scrub(info, new Dictionary<string, string> { ["REDIS_URL"] = "redis://tests:6379" });

        info.Environment["REDIS_URL"].Should().Be("redis://tests:6379");
    }

    [Fact]
    public async Task ProcessRunner_Step_DoesNotSeeTheAgentsRedisUrl()
    {
        Environment.SetEnvironmentVariable("REDIS_URL", "redis://agent-bus:6379");
        try
        {
            var lines = new List<string>();
            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                "/bin/sh", ["-c", "echo \"url=${REDIS_URL:-none}\""], "/", null, 10);

            await new ProcessRunner().RunAsync(step, (_, line) => lines.Add(line), CancellationToken.None);

            lines.Should().Contain("url=none");
        }
        finally
        {
            Environment.SetEnvironmentVariable("REDIS_URL", null);
        }
    }
}
