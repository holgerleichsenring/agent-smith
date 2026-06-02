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
            new SandboxSpec(ToolchainImage: "node:20", Resources: ResourceLimits.Default, AgentImage: "agent:1"));

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
            new SandboxSpec("img", ResourceLimits.Default, "ai"));

        spec.Cmd.Should().BeEquivalentTo("/shared/agent", "--redis-url", "redis:6379", "--job-id", "job-abc");
        spec.Env.Should().Contain("JOB_ID=job-abc");
        spec.Env.Should().Contain("REDIS_URL=redis:6379");
    }

    [Theory]
    [InlineData("2Gi", 2L * 1024 * 1024 * 1024)]
    [InlineData("512Mi", 512L * 1024 * 1024)]
    [InlineData("1G", 1_000_000_000L)]
    [InlineData("256M", 256_000_000L)]
    public void BuildToolchain_ParsesMemoryLimit_ToBytes(string memoryString, long expectedBytes)
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job", "redis",
            new SandboxSpec("img", new ResourceLimits("250m", "1000m", "256Mi", memoryString), "ai"));

        spec.HostConfig.Memory.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("500m", 500_000_000L)]
    [InlineData("2", 2_000_000_000L)]
    [InlineData("1.5", 1_500_000_000L)]
    public void BuildToolchain_ParsesCpuLimit_ToNanoCpus(string cpuString, long expectedNanoCpus)
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job", "redis",
            new SandboxSpec("img", new ResourceLimits("100m", cpuString, "256Mi", "2Gi"), "ai"));

        spec.HostConfig.NanoCPUs.Should().Be(expectedNanoCpus);
    }

    [Fact]
    public void BuildToolchain_MapsMemoryRequest_ToMemoryReservation()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job", "redis",
            new SandboxSpec("img", new ResourceLimits("250m", "1000m", "768Mi", "2Gi"), "ai"));

        spec.HostConfig.MemoryReservation.Should().Be(768L * 1024 * 1024);
    }

    // p0201: ownership + run-scope labels are the surface the orphan reaper
    // filters on and the watcher reads to cancel the right run-id. Both must
    // be present whenever a SandboxSpec carries a RunId.
    [Fact]
    public void BuildToolchain_TagsContainerWithJobIdAndRunIdLabels()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job-abc", "redis",
            new SandboxSpec("img", ResourceLimits.Default, "ai", RunId: "run-xyz"));

        spec.Labels.Should().ContainKey(DockerContainerSpecBuilder.JobIdLabel)
            .WhoseValue.Should().Be("job-abc");
        spec.Labels.Should().ContainKey(DockerContainerSpecBuilder.RunIdLabel)
            .WhoseValue.Should().Be("run-xyz");
    }

    [Fact]
    public void BuildToolchain_NullRunId_OmitsRunIdLabel_KeepsJobIdLabel()
    {
        var spec = _builder.BuildToolchain(
            "tc-1", "s", "w", "job-abc", "redis",
            new SandboxSpec("img", ResourceLimits.Default, "ai"));

        spec.Labels.Should().ContainKey(DockerContainerSpecBuilder.JobIdLabel);
        spec.Labels.Should().NotContainKey(DockerContainerSpecBuilder.RunIdLabel);
    }
}
