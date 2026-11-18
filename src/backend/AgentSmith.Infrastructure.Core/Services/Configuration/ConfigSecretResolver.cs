using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: substitutes every <c>${NAME}</c> reference the raw configuration carries, in the
/// one order that works — the secrets map first (its values come from the environment), then
/// the places that reference a secret BY name. Extracted from RawConfigMaterializer, which
/// orchestrates the raw-to-typed pipeline; which keys carry a reference is a separate reason
/// to change, and p0506 had to touch it once already.
/// </summary>
public sealed class ConfigSecretResolver(ConfigSecretReferences references, IStoredSecretValues? stored = null)
{
    public void Apply(RawAgentSmithConfig raw)
    {
        ResolveSecrets(raw);
        ResolveReferences(raw);
    }

    // 2026-11-17-f3a8d: the environment wins when it holds a value; otherwise the value stored in the admin area.
    // Stored values are opened once per load, so a run and the masker reading this load see the same generation.
    // 2026-11-18-78efb: a secret left with no value whose stored row exists but did not open is listed with why.
    private void ResolveSecrets(RawAgentSmithConfig raw)
    {
        var load = stored?.Load() ?? StoredSecretLoad.Empty;
        var resolved = new Dictionary<string, string>();
        var unreadable = new Dictionary<string, string>();
        foreach (var (key, value) in raw.Secrets)
        {
            var fromEnvironment = references.ResolveFromEnvironment(value);
            if (!string.IsNullOrEmpty(fromEnvironment)) resolved[key] = fromEnvironment;
            else if (load.Values.TryGetValue(key.ToLowerInvariant(), out var storedValue)) resolved[key] = storedValue;
            else
            {
                resolved[key] = fromEnvironment;
                if (load.Unreadable.TryGetValue(key.ToLowerInvariant(), out var reason)) unreadable[key] = reason;
            }
        }
        raw.Secrets = resolved;
        raw.UnreadableSecrets = unreadable;
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
        // 2026-10-29-6bd3a: on the tracker now — LegacyTriggerFold moved every jira_trigger.secret there.
        foreach (var tracker in raw.Trackers.Values)
            if (tracker.WebhookSecret is { } secret)
                tracker.WebhookSecret = references.Resolve(secret, raw.Secrets);
    }
}
