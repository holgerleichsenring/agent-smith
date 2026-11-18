using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8d: a secret's value — the environment's when it holds one, else the stored one.</summary>
public sealed class StoredSecretResolutionTests
{
    [Theory]
    [InlineData("from-env-value", "from-env-value")]
    [InlineData(null, "stored-value")]
    public void Apply_EnvironmentWinsOverTheStoredValue(string? environment, string expected)
    {
        var stored = new Mock<IStoredSecretValues>();
        stored.Setup(s => s.Load()).Returns(new Dictionary<string, string> { ["gh_token"] = "stored-value" });
        var raw = new RawAgentSmithConfig { Secrets = new Dictionary<string, string> { ["GH_TOKEN"] = "${GH_TOKEN}" } };

        new ConfigSecretResolver(new ConfigSecretReferences(_ => environment), stored.Object).Apply(raw);

        raw.Secrets["GH_TOKEN"].Should().Be(expected);
    }

    [Fact]
    public void Apply_NeitherEnvironmentNorStored_IsEmpty()
    {
        var raw = new RawAgentSmithConfig { Secrets = new Dictionary<string, string> { ["X"] = "${X}" } };

        new ConfigSecretResolver(new ConfigSecretReferences(_ => null)).Apply(raw);

        raw.Secrets["X"].Should().BeEmpty();
    }
}
