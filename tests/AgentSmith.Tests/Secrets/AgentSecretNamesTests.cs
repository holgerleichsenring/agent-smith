using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Secrets;

/// <summary>
/// 2026-09-23-4722b: an agent may name any environment variable as its api_key_secret, and the
/// spawners used to forward a hardcoded list — so a second seat, or any non-canonical key,
/// authenticated in-process and silently failed inside a spawned sandbox.
/// </summary>
public sealed class AgentSecretNamesTests
{
    private static AgentSmithConfig ConfigWith(params (string Agent, string? Secret)[] agents)
    {
        var config = AgentSmithConfig.Empty();
        foreach (var (name, secret) in agents)
            config.Agents[name] = new AgentConfig { Type = "copilot", Model = "gpt-5", ApiKeySecret = secret };
        return config;
    }

    [Fact]
    public void AgentNamesANonCanonicalSecret_IncludesItBesideTheCanonicalKeys()
    {
        var names = AgentSecretNames.For(ConfigWith(("team-a", "COPILOT_TOKEN_TEAM_A")));

        names.Should().Contain("COPILOT_TOKEN_TEAM_A");
        names.Should().Contain(AgentEnvKeys.AnthropicApiKey, "the canonical set is still forwarded");
        names.Should().Contain(AgentEnvKeys.RedisUrl);
    }

    [Fact]
    public void TwoAgentsNameTwoSecrets_IncludesBoth()
    {
        // The case Copilot makes ordinary: a seat belongs to a person, so two seats are two agents.
        var names = AgentSecretNames.For(ConfigWith(
            ("team-a", "COPILOT_TOKEN_TEAM_A"), ("team-b", "COPILOT_TOKEN_TEAM_B")));

        names.Should().Contain(["COPILOT_TOKEN_TEAM_A", "COPILOT_TOKEN_TEAM_B"]);
    }

    [Fact]
    public void CanonicalKeysOmittedByTheOldHardcodedList_AreIncluded()
    {
        // AZURE_OPENAI_API_KEY and GROQ_API_KEY are canonical and were missing from the Docker
        // spawner's literal list — evidence that a literal list drifts.
        var names = AgentSecretNames.For(AgentSmithConfig.Empty());

        names.Should().Contain([AgentEnvKeys.AzureOpenAiApiKey, AgentEnvKeys.GroqApiKey]);
    }

    [Fact]
    public void AgentNamesTheSameSecretTwice_ListsItOnce()
    {
        var names = AgentSecretNames.For(ConfigWith(("a", "SHARED_SEAT"), ("b", "SHARED_SEAT")));

        names.Count(n => n == "SHARED_SEAT").Should().Be(1);
    }

    [Fact]
    public void NoConfiguredAgents_IsJustTheCanonicalSet()
    {
        AgentSecretNames.For(null).Should().Equal(AgentSecretBinding.All.Select(b => b.EnvVar));
    }

    [Fact]
    public void NameOutsideTheSecretKeyCharset_IsRefusedByName()
    {
        // Refused here rather than at spawn time, where it would surface as an invalid pod spec.
        var act = () => AgentSecretNames.For(ConfigWith(("bad", "COPILOT TOKEN!")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*COPILOT TOKEN!*");
    }

    [Fact]
    public void CanonicalBindings_KeepTheirDeployedKeys()
    {
        // These are the contract of a secret an operator has already applied; re-deriving them
        // would rename live keys.
        AgentSecretNames.K8sSecretKeyFor(AgentEnvKeys.AnthropicApiKey).Should().Be("anthropic-api-key");
        AgentSecretNames.K8sSecretKeyFor(AgentEnvKeys.JiraEmail).Should().Be("jira-email");
    }

    [Fact]
    public void ConfiguredName_DerivesItsKeyFromTheVariable()
    {
        AgentSecretNames.K8sSecretKeyFor("COPILOT_TOKEN_TEAM_A").Should().Be("copilot-token-team-a");
    }
}
