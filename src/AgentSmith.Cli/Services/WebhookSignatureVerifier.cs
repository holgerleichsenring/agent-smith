using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Cli.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Validates webhook request signatures by delegating to
/// platform-specific validators.
/// </summary>
internal sealed class WebhookSignatureVerifier(
    IServiceProvider services,
    ILogger logger)
{
    public bool Validate(
        string platform, string body, IDictionary<string, string> headers)
    {
        return platform switch
        {
            "github" => !headers.TryGetValue("X-Hub-Signature-256", out var sig)
                        || WebhookSignatureValidator.ValidateGitHub(body, sig,
                            Environment.GetEnvironmentVariable("GITHUB_WEBHOOK_SECRET") ?? ""),
            "gitlab" => !headers.TryGetValue("X-Gitlab-Token", out var token)
                        || WebhookSignatureValidator.ValidateGitLab(token,
                            Environment.GetEnvironmentVariable("GITLAB_WEBHOOK_TOKEN") ?? ""),
            "azuredevops" => !headers.TryGetValue("Authorization", out var auth)
                        || WebhookSignatureValidator.ValidateAzureDevOps(auth,
                            Environment.GetEnvironmentVariable("AZDO_WEBHOOK_SECRET") ?? ""),
            "jira" => ValidateJira(body, headers),
            _ => true
        };
    }

    private bool ValidateJira(string body, IDictionary<string, string> headers)
    {
        try
        {
            var configLoader = services.GetService<IConfigurationLoader>();
            var serverCtx = services.GetService<ServerContext>();
            if (configLoader is null || serverCtx is null)
                return true;

            var config = configLoader.LoadConfig(serverCtx.ConfigPath);

            // Jira Cloud system webhooks don't send a signature header.
            // Only validate if both a secret is configured AND a signature header is present.
            headers.TryGetValue("x-hub-signature", out var sig);
            if (sig is null)
                return true;

            foreach (var (_, project) in config.Projects)
            {
                var secret = project.JiraTrigger?.Secret;
                if (string.IsNullOrEmpty(secret)) continue;

                return WebhookSignatureValidator.ValidateJira(body, sig, secret);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate Jira webhook signature");
            return false;
        }
    }
}
