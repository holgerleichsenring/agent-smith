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
        stored.Setup(s => s.Load()).Returns(new StoredSecretLoad(new Dictionary<string, string> { ["gh_token"] = "stored-value" }, new Dictionary<string, string>()));
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
        raw.UnreadableSecrets.Should().BeEmpty("nothing is stored, which is not the same as unreadable");
    }

    // 2026-11-18-78efb: a stored row that exists but did not open is listed with its value-free reason — unless the
    // environment holds a value, which wins and leaves nothing unreadable.
    [Theory]
    [InlineData(null, true)]
    [InlineData("from-env-value", false)]
    public void Apply_AnUnreadableStoredRow_IsListedOnlyWhenTheEnvironmentHoldsNothing(string? environment, bool listed)
    {
        var stored = new Mock<IStoredSecretValues>();
        stored.Setup(s => s.Load()).Returns(new StoredSecretLoad(new Dictionary<string, string>(),
            new Dictionary<string, string> { ["github_webhook_secret"] = "no master key is configured (key env:1)" }));
        var raw = new RawAgentSmithConfig { Secrets = new Dictionary<string, string> { ["GITHUB_WEBHOOK_SECRET"] = "${GITHUB_WEBHOOK_SECRET}" } };

        new ConfigSecretResolver(new ConfigSecretReferences(_ => environment), stored.Object).Apply(raw);

        if (listed) raw.UnreadableSecrets.Should().ContainKey("GITHUB_WEBHOOK_SECRET").WhoseValue.Should().Contain("no master key");
        else raw.UnreadableSecrets.Should().BeEmpty();
    }
}
