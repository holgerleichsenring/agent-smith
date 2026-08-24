using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Webhooks;

/// <summary>
/// p0506: the single platform-to-secret table. The environment read is injected, so a
/// test states what is configured instead of mutating the process every other test in
/// the suite shares.
/// </summary>
public sealed class WebhookSecretResolver(Func<string, string?> envReader) : IWebhookSecretResolver
{
    // Jira's entry has no environment variable on purpose: its secret is per project,
    // on jira_trigger.secret, and several projects may each carry a different one.
    private static readonly (string Platform, string? EnvVar)[] KnownPlatforms =
    [
        ("github", "GITHUB_WEBHOOK_SECRET"),
        ("gitlab", "GITLAB_WEBHOOK_TOKEN"),
        ("azuredevops", "AZDO_WEBHOOK_SECRET"),
        ("jira", null),
    ];

    public WebhookSecretSource? Resolve(string platform, AgentSmithConfig config)
    {
        foreach (var (known, envVar) in KnownPlatforms)
        {
            if (!string.Equals(known, platform, StringComparison.OrdinalIgnoreCase)) continue;
            return new WebhookSecretSource(known, envVar, Secrets(envVar, config));
        }
        return null;
    }

    public IReadOnlyList<WebhookSecretSource> ResolveAll(AgentSmithConfig config) =>
        [.. KnownPlatforms.Select(p => new WebhookSecretSource(p.Platform, p.EnvVar, Secrets(p.EnvVar, config)))];

    private IReadOnlyList<string> Secrets(string? envVar, AgentSmithConfig config) =>
        envVar is null
            ? [.. config.Projects.Values
                .Select(p => p.JiraTrigger?.Secret)
                .Where(secret => !string.IsNullOrEmpty(secret))
                .Select(secret => secret!)]
            : envReader(envVar) is { Length: > 0 } fromEnvironment ? [fromEnvironment] : [];
}
