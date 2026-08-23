using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// The whole authentication on the webhook routes: a delivery must prove it was sent by
/// the platform whose secret this deployment configured.
/// <para>
/// p0506: this used to answer <c>!headers.TryGetValue(...) || Validate(...)</c> per
/// platform — header absent, the disjunction short-circuited, the secret was never read.
/// A secret an operator HAD configured bought nothing: an unsigned POST reached the
/// handlers, and a forged PR-comment approval published an answer to a master blocked on
/// a question. The rule now lives in one place every platform passes through, and what a
/// platform requires comes from <see cref="IWebhookSecretResolver"/> rather than from an
/// env-var table copied into three files.
/// </para>
/// </summary>
internal sealed class WebhookSignatureVerifier(
    IServiceProvider services,
    ILogger logger)
{
    public bool Validate(
        string platform, string body, IDictionary<string, string> headers)
    {
        var source = ConfiguredSecrets(platform);
        if (source is null) return false;
        // p0506's fail-open rule: no secret configured anywhere for this platform means
        // the deployment never set one up, and every shipped template is in that state.
        // Refusing here would 401 them all on upgrade.
        if (!source.IsConfigured) return true;
        return source.Secrets.Any(secret => Matches(platform, body, headers, secret));
    }

    // An unreadable configuration is not evidence that no secret is configured, and an
    // unknown platform is not a platform we can check — both are refusals.
    private WebhookSecretSource? ConfiguredSecrets(string platform)
    {
        var resolver = services.GetService<IWebhookSecretResolver>();
        var config = LoadConfig();
        if (resolver is null || config is null)
        {
            logger.LogWarning(
                "Cannot tell whether a webhook secret is configured for {Platform} — refusing", platform);
            return null;
        }

        var source = resolver.Resolve(platform, config);
        if (source is null)
            logger.LogWarning("Webhook delivery for unknown platform {Platform} — refusing", platform);
        return source;
    }

    private AgentSmithConfig? LoadConfig()
    {
        var configLoader = services.GetService<IConfigurationLoader>();
        var serverContext = services.GetService<ServerContext>();
        if (configLoader is null || serverContext is null) return null;
        try
        {
            return configLoader.LoadConfig(serverContext.ConfigPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the configuration for webhook signature verification");
            return null;
        }
    }

    // Jira is n-valued: several projects may each carry their own secret, so a delivery
    // is accepted when ANY configured secret validates it. Before p0506 the loop returned
    // on the FIRST project holding one, refusing every other project's webhook.
    private static bool Matches(
        string platform, string body, IDictionary<string, string> headers, string secret) =>
        platform switch
        {
            "github" => headers.TryGetValue("X-Hub-Signature-256", out var signature)
                        && WebhookSignatureValidator.ValidateGitHub(body, signature, secret),
            "gitlab" => headers.TryGetValue("X-Gitlab-Token", out var token)
                        && WebhookSignatureValidator.ValidateGitLab(token, secret),
            "azuredevops" => headers.TryGetValue("Authorization", out var authorization)
                        && WebhookSignatureValidator.ValidateAzureDevOps(authorization, secret),
            "jira" => headers.TryGetValue("x-hub-signature", out var jiraSignature)
                        && WebhookSignatureValidator.ValidateJira(body, jiraSignature, secret),
            _ => false,
        };
}
