using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0336c: the per-field max of a set of limits — the envelope for one sandbox
/// hosting several same-toolchain contexts.
/// </summary>
public sealed class ResourceEnvelopeTests
{
    [Fact]
    public void Max_TakesLargestValuePerField_KeepingOriginalStrings()
    {
        var result = ResourceEnvelope.Max(new[]
        {
            new ResourceLimits("250m", "1", "1Gi", "3Gi"),
            new ResourceLimits("500m", "2", "512Mi", "4Gi"),
        });

        result.CpuRequest.Should().Be("500m");
        result.CpuLimit.Should().Be("2");
        result.MemoryRequest.Should().Be("1Gi");
        result.MemoryLimit.Should().Be("4Gi");
    }

    [Fact]
    public void Max_SingleMember_ReturnsItUnchanged()
    {
        var only = new ResourceLimits("100m", "500m", "128Mi", "1Gi");
        ResourceEnvelope.Max(new[] { only }).Should().BeSameAs(only);
    }
}
