using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class DockerContainerSpecBuilderTests
{
    private readonly DockerContainerSpecBuilder _builder = new();

    [Fact]
    public void BuildLoader_AlwaysProducesInjectCommand_AgainstSharedMount()
    {
        var spec = _builder.BuildLoader("loader-c1", "shared-vol-1", "agent:1.2.3");

        spec.Image.Should().Be("agent:1.2.3");
        spec.Cmd.Should().BeEquivalentTo("--inject", "/shared/agent");
        spec.HostConfig.Binds.Should().ContainSingle().Which.Should().Be("shared-vol-1:/shared");
    }

    [Fact]
    public void BuildToolchain_BindsBothVolumes_SharedReadOnly_WorkReadWrite()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "shared-vol-1", "work-vol-1", "job-abc",
            "redis:6379",
            new SandboxSpec(ToolchainImage: "node:20", AgentImage: "agent:1"));

        spec.Image.Should().Be("node:20");
        spec.WorkingDir.Should().Be("/work");
        spec.HostConfig.Binds.Should().BeEquivalentTo(
            "shared-vol-1:/shared:ro",
            "work-vol-1:/work");
    }

    [Fact]
    public void BuildToolchain_PassesJobIdAndRedisUrl_AsCmdArgsAndEnv()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job-abc", "redis:6379",
            new SandboxSpec("img", "ai"));

        spec.Cmd.Should().BeEquivalentTo("/shared/agent", "--redis-url", "redis:6379", "--job-id", "job-abc");
        spec.Env.Should().Contain("JOB_ID=job-abc");
        spec.Env.Should().Contain("REDIS_URL=redis:6379");
    }

    [Theory]
    [InlineData("2Gi", 2L * 1024 * 1024 * 1024)]
    [InlineData("512Mi", 512L * 1024 * 1024)]
    [InlineData("1G", 1_000_000_000L)]
    [InlineData("256M", 256_000_000L)]
    public void BuildToolchain_ParsesMemory_ToBytes(string memoryString, long expectedBytes)
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job", "redis",
            new SandboxSpec("img", "ai", Resources: new ResourceLimits(Memory: memoryString)));

        spec.HostConfig.Memory.Should().Be(expectedBytes);
    }

    [Fact]
    public void BuildToolchain_ConvertsCpuCoresToNanoCpus()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job", "redis",
            new SandboxSpec("img", "ai", Resources: new ResourceLimits(CpuCores: 1.5)));

        spec.HostConfig.NanoCPUs.Should().Be(1_500_000_000L);
    }
}
