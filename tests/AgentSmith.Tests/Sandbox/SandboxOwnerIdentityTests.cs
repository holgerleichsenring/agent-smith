using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0465: a sandbox belongs to the LIVENESS STORE its owner judges against, so the
/// identity is derived from the Redis endpoint the sandbox itself is handed — not
/// from the process, host or pod, which would make a server's own containers foreign
/// to it after a restart. It must be a legal Kubernetes label value by construction,
/// or the Docker and Kubernetes backends silently disagree about who owns what.
/// </summary>
public sealed class SandboxOwnerIdentityTests
{
    private readonly SandboxOwnerIdentityResolver _resolver = new();

    [Theory]
    [InlineData("redis:6379")]
    [InlineData("localhost:6379,abortConnect=false")]
    [InlineData("rediss://user:pw@cache.example.internal:6380/3")]
    [InlineData("not a redis url at all")]
    [InlineData("")]
    public void Resolve_AlwaysProducesALegalKubernetesLabelValue(string redisUrl)
    {
        var value = _resolver.Resolve(redisUrl, operatorOverride: null).Value;

        value.Should().MatchRegex("^[A-Za-z0-9]([A-Za-z0-9._-]*[A-Za-z0-9])?$");
        value.Length.Should().BeLessThanOrEqualTo(63);
    }

    [Fact]
    public void Resolve_SameStore_SameIdentity_SoTwoReplicasCleanUpAfterEachOther()
    {
        // p0355 must survive: two servers on ONE store are one owner.
        _resolver.Resolve("redis:6379", null)
            .Should().Be(_resolver.Resolve("redis:6379", null));
    }

    [Fact]
    public void Resolve_IgnoresConnectionOptions_TheEndpointAndDatabaseAreTheStore()
    {
        _resolver.Resolve("redis:6379", null)
            .Should().Be(_resolver.Resolve("redis:6379,abortConnect=false,connectTimeout=5000", null));
    }

    [Fact]
    public void Resolve_DifferentStore_DifferentIdentity()
    {
        _resolver.Resolve("redis:6379", null)
            .Should().NotBe(_resolver.Resolve("other-redis:6379", null));
    }

    [Fact]
    public void Resolve_DifferentDatabaseOnTheSameHost_DifferentIdentity()
    {
        _resolver.Resolve("redis:6379,defaultDatabase=0", null)
            .Should().NotBe(_resolver.Resolve("redis:6379,defaultDatabase=7", null));
    }

    [Fact]
    public void Resolve_OperatorOverride_WinsAndIsUsedVerbatimWhenItIsALegalLabel()
    {
        _resolver.Resolve("redis:6379", "team-blue-server").Value.Should().Be("team-blue-server");
    }

    [Fact]
    public void Resolve_IllegalOverride_IsFoldedToALegalLabelRatherThanRejected()
    {
        var value = _resolver.Resolve("redis:6379", new string('x', 200) + " /illegal/").Value;

        value.Should().MatchRegex("^[A-Za-z0-9]([A-Za-z0-9._-]*[A-Za-z0-9])?$");
        value.Length.Should().BeLessThanOrEqualTo(63);
    }
}
