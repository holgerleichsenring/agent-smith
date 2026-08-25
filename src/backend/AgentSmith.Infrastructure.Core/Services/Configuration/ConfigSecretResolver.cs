using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: substitutes every <c>${NAME}</c> reference the raw configuration carries, in the
/// one order that works — the secrets map first (its values come from the environment), then
/// the places that reference a secret BY name. Extracted from RawConfigMaterializer, which
/// orchestrates the raw-to-typed pipeline; which keys carry a reference is a separate reason
/// to change, and p0506 had to touch it once already.
/// </summary>
public sealed class ConfigSecretResolver(ConfigSecretReferences references)
{
    public void Apply(RawAgentSmithConfig raw)
    {
        ResolveSecrets(raw);
        ResolveReferences(raw);
    }

    private void ResolveSecrets(RawAgentSmithConfig raw)
    {
        var resolved = new Dictionary<string, string>();
        foreach (var (key, value) in raw.Secrets)
            resolved[key] = references.ResolveFromEnvironment(value);
        raw.Secrets = resolved;
    }

    // p0191: registry tokens reference secrets via ${name}; substitute them after
    // ResolveSecrets has replaced the secrets-dict values with env-var contents.
    // p0506: a project's jira_trigger.secret carries the same reference shape — the
    // shipped example writes ${JIRA_WEBHOOK_SECRET} — and was substituted by nothing,
    // so the verifier compared deliveries against the literal placeholder.
    private void ResolveReferences(RawAgentSmithConfig raw)
    {
        foreach (var entry in raw.Registries)
            entry.Token = references.Resolve(entry.Token, raw.Secrets);
        foreach (var project in raw.Projects.Values)
            if (project.JiraTrigger?.Secret is { } secret)
                project.JiraTrigger.Secret = references.Resolve(secret, raw.Secrets);
    }
}
