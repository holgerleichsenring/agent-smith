using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>
/// 2026-11-18-9ae8: a credential named by its environment variable — an agent key, a webhook or chat adapter secret —
/// resolves the variable first and the catalog secret of that name (whose value may be stored in the admin area) second.
/// </summary>
public sealed class CredentialResolverNamedTests
{
    private static readonly Dictionary<string, string> NoEnvironment = new();

    [Fact]
    public void Named_EnvironmentWins_ThenTheCatalog_ThenNothing()
    {
        var catalog = ("anthropic_api_key", "from-the-store");

        TestCredentials.Isolated(new() { ["ANTHROPIC_API_KEY"] = "from-env" }, catalog).Named("ANTHROPIC_API_KEY").Should().Be("from-env");
        TestCredentials.Isolated(new() { ["ANTHROPIC_API_KEY"] = "" }, catalog).Named("ANTHROPIC_API_KEY").Should().Be("from-the-store");
        TestCredentials.Isolated(NoEnvironment).Named("ANTHROPIC_API_KEY").Should().BeNull();
        TestCredentials.Isolated(NoEnvironment, catalog).Named("").Should().BeNull();
    }

    [Fact]
    public void Builders_SendAKeyOnlyTheCatalogHolds()
    {
        var credentials = TestCredentials.Isolated(NoEnvironment,
            ("anthropic_api_key", "sk-ant-api03-catalog"), ("my_gemini", "gemini-catalog"), ("copilot_github_token", "gho-catalog"));

        ClaudeChatClientBuilder.ResolveApiKey(credentials, new AgentConfig()).Should().Be("sk-ant-api03-catalog");
        new AgentApiKey(credentials).Resolve(new AgentConfig { ApiKeySecret = "MY_GEMINI" }, AgentEnvKeys.GeminiApiKey).Should().Be("gemini-catalog");
        var build = () => new GeminiChatClientBuilder(credentials).Build(
            new AgentConfig { Type = "gemini", ApiKeySecret = "my_gemini" }, new ModelAssignment { Model = "gemini-2.5-pro" });
        build.Should().NotThrow();
        new AgentApiKey(credentials).Resolve(new AgentConfig(), AgentEnvKeys.CopilotGitHubToken).Should().Be("gho-catalog");
    }

    [Fact]
    public void OpenAi_ANamedKeyWithNoValue_IsRefusedByName()
    {
        var build = () => new OpenAiChatClientBuilder(TestCredentials.Isolated(NoEnvironment)).Build(
            new AgentConfig { Type = "openai", Endpoint = "http://localhost:9/v1", ApiKeySecret = "LOCAL_KEY" },
            new ModelAssignment { Model = "m" });

        build.Should().Throw<InvalidOperationException>().WithMessage("*'LOCAL_KEY'*environment or the secret store*");
    }

    [Fact]
    public void RateBudget_ReadsTheAgentsResolvedAnthropicKey()
    {
        var agent = new AgentConfig();

        LlmRateBudget.For(agent, "claude", () => "sk-ant-oat-subscription").RequestsPerMinute.Should().Be(5);
        LlmRateBudget.For(agent, "claude", () => "sk-ant-api03-key").RequestsPerMinute.Should().Be(50);
        LlmRateBudget.For(agent, "openai", () => throw new InvalidOperationException("not asked")).RequestsPerMinute.Should().Be(60);
    }

    [Fact]
    public void WebhookSecret_EnvironmentWins_TheCatalogFillsAnEmptyVariable()
    {
        var config = TestCredentials.Config(("github_webhook_secret", "catalog-hook"));

        new WebhookSecretResolver(_ => null).Resolve("github", config)!.Secrets.Should().Equal("catalog-hook");
        new WebhookSecretResolver(n => n == "GITHUB_WEBHOOK_SECRET" ? "env-hook" : null).Resolve("github", config)!
            .Secrets.Should().Equal("env-hook");
        new WebhookSecretResolver(_ => null).Resolve("gitlab", config)!.Secrets.Should().BeEmpty();
    }

    [Fact]
    public void ChatAdapterOptions_ReadOnEveryAccess_AnAssignedValueWins()
    {
        var current = "first-token";
        var slack = new SlackAdapterOptions(name => name == SlackAdapterOptions.BotTokenVariable ? current : null);
        slack.BotToken.Should().Be("first-token");
        current = "second-token";
        slack.BotToken.Should().Be("second-token");
        slack.SigningSecret.Should().BeEmpty();
        slack.BotToken = "assigned";
        slack.BotToken.Should().Be("assigned");

        new TeamsAdapterOptions(name => name == TeamsAdapterOptions.AppPasswordVariable ? "pw-123456" : null)
            .AppPassword.Should().Be("pw-123456");
    }
}
