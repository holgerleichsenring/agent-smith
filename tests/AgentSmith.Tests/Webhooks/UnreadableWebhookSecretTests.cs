using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// 2026-11-18-78efb: a platform secret whose stored value exists but cannot be opened refuses every delivery and says
/// which secret and why; a platform with no value configured at all still falls open (p0506).
/// </summary>
public sealed class UnreadableWebhookSecretTests
{
    private const string Body = """{"action":"labeled"}""";
    private const string Reason = "it did not open under its key (tampered or wrapped by another key) (key env:1)";

    [Fact]
    public void Resolve_AnUnreadableCatalogSecret_IsSaidAsUnreadable_NotAsUnconfigured()
    {
        var source = new WebhookSecretResolver(_ => null).Resolve("github", Unreadable("GITHUB_WEBHOOK_SECRET"))!;

        source.IsConfigured.Should().BeFalse();
        source.Unreadable.Should().Contain("GITHUB_WEBHOOK_SECRET").And.Contain("cannot be read").And.Contain("env:1");
    }

    [Fact]
    public void Resolve_TheEnvironmentHoldingAValue_Wins()
    {
        var source = new WebhookSecretResolver(_ => "from-env-value").Resolve("github", Unreadable("GITHUB_WEBHOOK_SECRET"))!;

        source.Unreadable.Should().BeNull();
        source.Secrets.Should().Equal("from-env-value");
    }

    [Fact]
    public void Validate_AnUnreadableSecret_RefusesEveryDelivery_AndTheLogNamesTheSecretAndWhy()
    {
        var logs = new RecordingLoggerProvider();
        var verifier = new WebhookSignatureVerifier(Services(Unreadable("GITLAB_WEBHOOK_TOKEN")), logs.CreateLogger("webhooks"));

        verifier.Validate("gitlab", Body, Headers(("X-Gitlab-Token", "anything"))).Should().BeFalse();
        verifier.Validate("gitlab", Body, Headers()).Should().BeFalse();
        logs.Entries.Should().Contain(e => e.Level == LogLevel.Error
            && e.Message.Contains("GITLAB_WEBHOOK_TOKEN") && e.Message.Contains("tampered"));
    }

    [Fact]
    public void Validate_NoValueConfiguredAnywhere_StillFallsOpen()
    {
        var verifier = new WebhookSignatureVerifier(Services(new AgentSmithConfig()), new RecordingLoggerProvider().CreateLogger("w"));

        verifier.Validate("azuredevops", Body, Headers()).Should().BeTrue();
    }

    private static AgentSmithConfig Unreadable(string secret) => new()
    {
        Secrets = new Dictionary<string, string> { [secret] = string.Empty },
        UnreadableSecrets = new Dictionary<string, string> { [secret] = Reason },
    };

    private static IServiceProvider Services(AgentSmithConfig config) => new ServiceCollection()
        .AddSingleton<IWebhookSecretResolver>(new WebhookSecretResolver(_ => null))
        .AddSingleton(new ServerContext("agentsmith.yml"))
        .AddSingleton<IConfigurationLoader>(new FixedLoader(config))
        .BuildServiceProvider();

    private static Dictionary<string, string> Headers(params (string Key, string Value)[] headers) =>
        headers.ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);

    private sealed class FixedLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) => config;
    }
}
