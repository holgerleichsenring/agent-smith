using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0268: the shared quantity parser the Docker spawner uses to size containers
/// is the SAME parser the resolver uses to validate LLM-authored context.yaml
/// quantities. Pins the suffix handling (cores/millicores, Gi/Mi/Ki/G/M/K) and
/// that garbage / non-positive values are rejected so the resolver falls back.
/// </summary>
public sealed class KubernetesQuantityTests
{
    [Theory]
    [InlineData("2", 2_000_000_000L)]
    [InlineData("500m", 500_000_000L)]
    [InlineData("1000m", 1_000_000_000L)]
    public void TryParseCpuToNanoCpus_ValidQuantities_Parse(string raw, long expected)
    {
        KubernetesQuantity.TryParseCpuToNanoCpus(raw, out var nanoCpus).Should().BeTrue();
        nanoCpus.Should().Be(expected);
    }

    [Theory]
    [InlineData("4Gi", 4L * 1024 * 1024 * 1024)]
    [InlineData("512Mi", 512L * 1024 * 1024)]
    [InlineData("256Mi", 256L * 1024 * 1024)]
    public void TryParseMemoryToBytes_ValidQuantities_Parse(string raw, long expected)
    {
        KubernetesQuantity.TryParseMemoryToBytes(raw, out var bytes).Should().BeTrue();
        bytes.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lots")]
    [InlineData("0")]
    [InlineData("abc500m")]
    public void TryParseCpuToNanoCpus_Garbage_Rejected(string? raw)
    {
        KubernetesQuantity.TryParseCpuToNanoCpus(raw, out var nanoCpus).Should().BeFalse();
        nanoCpus.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plenty")]
    [InlineData("0Mi")]
    [InlineData("4GG")]
    public void TryParseMemoryToBytes_Garbage_Rejected(string? raw)
    {
        KubernetesQuantity.TryParseMemoryToBytes(raw, out var bytes).Should().BeFalse();
        bytes.Should().Be(0);
    }
}
