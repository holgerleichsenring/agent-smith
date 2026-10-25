using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class InProcessStepEnvironmentTests
{
    [Fact]
    public void InProcessStepEnvironment_DeclaredName_IsRemoved()
    {
        var config = new AgentSmithConfig();
        config.Agents["own"] = new AgentConfig { Type = "claude", ApiKeySecret = "MY_TEAM_MODEL_KEY" };
        var environment = Host(("REDIS_URL", "redis:6379,password=x"), ("ANTHROPIC_API_KEY", "sk-1"),
            ("JIRA_WEBHOOK_SECRET", "hook"), ("MY_TEAM_MODEL_KEY", "k"), ("PATH", "/usr/bin"));

        InProcessStepEnvironment.For(config, []).Scrub(environment);

        environment.Keys.Should().Equal("PATH");
    }

    [Fact]
    public void InProcessStepEnvironment_VariableHoldingAKnownSecretValue_IsRemoved()
    {
        var environment = Host(("NUGET_FEED_PASSWORD", "registry-token-123"), ("CI", "1"), ("DOTNET_NOLOGO", "true"));

        InProcessStepEnvironment.For(null, ["registry-token-123", "1", "true"]).Scrub(environment);

        environment.Keys.Should().BeEquivalentTo("CI", "DOTNET_NOLOGO");
    }

    [Fact]
    public void InProcessStepEnvironment_OrdinaryVariable_IsKept()
    {
        var environment = Host(("HOME", "/home/agent"), ("LANG", "C.UTF-8"));

        InProcessStepEnvironment.For(null, ["some-secret-value"]).Scrub(environment);

        environment.Should().HaveCount(2);
    }

    [Fact]
    public void InProcessStepEnvironment_Inherit_RemovesNothing()
    {
        var environment = Host(("REDIS_URL", "redis:6379"));

        InProcessStepEnvironment.Inherit.Scrub(environment);

        environment.Should().ContainKey("REDIS_URL");
    }

    private static Dictionary<string, string?> Host(params (string Name, string Value)[] variables) =>
        variables.ToDictionary(v => v.Name, v => (string?)v.Value);
}
