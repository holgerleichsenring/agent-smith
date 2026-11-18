namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0506: what one webhook platform's signature check compares a delivery against.
/// <see cref="EnvVar"/> is the environment variable the secret is read from, or null
/// for Jira, whose secret is per project on <c>jira_trigger.secret</c>. Jira is the
/// only n-valued platform: several projects may each carry their own secret.
/// 2026-11-18-78efb: <see cref="Unreadable"/> says, without the value, that the secret's stored value exists but cannot
/// be opened — the platform then refuses every delivery instead of falling open.
/// </summary>
public sealed record WebhookSecretSource(
    string Platform, string? EnvVar, IReadOnlyList<string> Secrets, string? Unreadable)
{
    /// <summary>Nothing configured means the platform falls open — see p0506's fail-open rule.</summary>
    public bool IsConfigured => Secrets.Count > 0;

    /// <summary>Secret values are held here; the generated text would print them.</summary>
    public override string ToString() =>
        $"{Platform} ({EnvVar ?? "per project"}): {Secrets.Count} secret(s){(Unreadable is null ? "" : ", " + Unreadable)}";
}
