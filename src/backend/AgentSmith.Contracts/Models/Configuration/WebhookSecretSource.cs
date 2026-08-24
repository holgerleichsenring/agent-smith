namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0506: what one webhook platform's signature check compares a delivery against.
/// <see cref="EnvVar"/> is the environment variable the secret is read from, or null
/// for Jira, whose secret is per project on <c>jira_trigger.secret</c>. Jira is the
/// only n-valued platform: several projects may each carry their own secret.
/// </summary>
public sealed record WebhookSecretSource(
    string Platform, string? EnvVar, IReadOnlyList<string> Secrets)
{
    /// <summary>Nothing configured means the platform falls open — see p0506's fail-open rule.</summary>
    public bool IsConfigured => Secrets.Count > 0;
}
