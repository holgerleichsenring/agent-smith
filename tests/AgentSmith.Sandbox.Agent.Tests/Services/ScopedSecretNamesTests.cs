using System.Text.RegularExpressions;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>2026-11-25-9992d: the scoped name the server writes is the one the agent reads back, whatever the
/// repository is called, and it is always a name Kubernetes accepts for an env variable.</summary>
public sealed class ScopedSecretNamesTests
{
    [Theory]
    [InlineData("api", "DB_PASSWORD")]
    [InlineData("billing-api", "NUGET_TOKEN")]
    [InlineData("web.frontend", "NPM_TOKEN")]
    [InlineData("acme/orders", "API_KEY")]
    [InlineData("a__b", "X")]
    [InlineData("büro", "TOKEN_2")]
    public void For_ThenTryParse_RoundTrips(string repository, string name)
    {
        var variable = ScopedSecretNames.For(repository, name);

        ScopedSecretNames.TryParse(variable, out var parsedRepository, out var parsedName).Should().BeTrue();
        parsedRepository.Should().Be(repository);
        parsedName.Should().Be(name);
        Regex.IsMatch(variable, "^[-._a-zA-Z][-._a-zA-Z0-9]*$").Should().BeTrue("Kubernetes refuses any other env name");
        ScopedSecretNames.IsScoped(variable).Should().BeTrue();
    }

    [Fact]
    public void For_SpellsTheRepositoryAsUppercaseHex()
    {
        ScopedSecretNames.For("api", "DB_PASSWORD").Should().Be("AGENTSMITH_SECRET_617069__DB_PASSWORD");
    }

    [Theory]
    [InlineData("FEED_TOKEN")]
    [InlineData("AGENTSMITH_SECRET_")]
    [InlineData("AGENTSMITH_SECRET_6170__")]
    [InlineData("AGENTSMITH_SECRET_617__X")]
    [InlineData("AGENTSMITH_SECRET_ZZ__X")]
    public void TryParse_ANameThatIsNotAScopedSecret_IsRefused(string variable)
    {
        ScopedSecretNames.TryParse(variable, out _, out _).Should().BeFalse();
    }
}
