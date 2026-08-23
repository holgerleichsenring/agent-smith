using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// p0506: a webhook route is the only unauthenticated entry point into the orchestrator,
/// and the signature check is its whole gate. It used to answer
/// <c>!headers.TryGetValue(...) || Validate(...)</c> — header absent, disjunction
/// short-circuits, the secret never read — so an unsigned POST carrying a forged event
/// header reached the handlers on a deployment that had configured a secret. A platform
/// with NO secret configured still falls open, because refusing everywhere would break
/// every template-following deployment on upgrade.
/// </summary>
public sealed class WebhookSignatureVerifierTests
{
    private const string Body = """{"action":"labeled"}""";

    [Fact]
    public void Validate_GithubSecretConfiguredAndNoHeader_IsRefused()
    {
        var verifier = Verifier(env: _ => "the-shared-secret");

        verifier.Validate("github", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_GitlabSecretConfiguredAndNoHeader_IsRefused()
    {
        var verifier = Verifier(env: _ => "the-shared-secret");

        verifier.Validate("gitlab", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_AzureDevopsSecretConfiguredAndNoHeader_IsRefused()
    {
        var verifier = Verifier(env: _ => "the-shared-secret");

        verifier.Validate("azuredevops", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_JiraSecretConfiguredAndNoHeader_IsRefused()
    {
        var verifier = Verifier(config: ConfigWithJiraSecrets("the-shared-secret"));

        verifier.Validate("jira", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_JiraSecondProjectHoldsTheMatchingSecret_IsAccepted()
    {
        var verifier = Verifier(config: ConfigWithJiraSecrets("another-project's", "ours"));

        verifier.Validate("jira", Body, Headers(("x-hub-signature", Sign(Body, "ours"))))
            .Should().BeTrue();
    }

    [Fact]
    public void Validate_JiraConfigurationUnreadable_IsRefused()
    {
        // The configuration is what says whether a secret is configured. Not being able
        // to read it is not evidence that none is.
        var verifier = new WebhookSignatureVerifier(
            Services(config: null, env: _ => null), NullLogger.Instance);

        verifier.Validate("jira", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_UnknownPlatform_IsRefused()
    {
        var verifier = Verifier(env: _ => "the-shared-secret");

        verifier.Validate("bitbucket", Body, Headers()).Should().BeFalse();
    }

    [Fact]
    public void Validate_NoSecretConfiguredAnywhere_IsAcceptedAsBefore()
    {
        // The fail-open guard: an upgrade must not 401 every deployment that follows the
        // shipped templates, none of which set a webhook secret.
        var verifier = Verifier(env: _ => null);

        verifier.Validate("github", Body, Headers()).Should().BeTrue();
    }

    private static WebhookSignatureVerifier Verifier(
        AgentSmithConfig? config = null, Func<string, string?>? env = null) =>
        new(Services(config ?? new AgentSmithConfig(), env ?? (_ => null)), NullLogger.Instance);

    private static IServiceProvider Services(AgentSmithConfig? config, Func<string, string?> env)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebhookSecretResolver>(new WebhookSecretResolver(env));
        services.AddSingleton(new ServerContext("agentsmith.yml"));
        if (config is not null)
            services.AddSingleton<IConfigurationLoader>(new FixedConfigurationLoader(config));
        return services.BuildServiceProvider();
    }

    private static AgentSmithConfig ConfigWithJiraSecrets(params string[] secrets) => new()
    {
        Projects = secrets
            .Select((secret, index) => (Name: $"p{index}", Secret: secret))
            .ToDictionary(p => p.Name, p => new ResolvedProject
            {
                Name = p.Name,
                JiraTrigger = new JiraTriggerConfig { Secret = p.Secret },
            }),
    };

    private static Dictionary<string, string> Headers(params (string Key, string Value)[] headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers) dict[key] = value;
        return dict;
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FixedConfigurationLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) => config;
    }
}
