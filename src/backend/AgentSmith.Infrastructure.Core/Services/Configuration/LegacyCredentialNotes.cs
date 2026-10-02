using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: what the legacy-credential migration tells an operator. Every note is
/// ADVISORY — the configuration it describes worked before and still works — and names a field
/// of its own, so a fill and an empty value on one entry are two lines, not one overwriting
/// the other. No note ever carries a secret's value.
/// </summary>
public static class LegacyCredentialNotes
{
    public static StartupFinding Filled(string field, string entity, LegacyCredentialKey key) =>
        Advisory($"{field}.auth",
            $"{entity} names no auth secret; it authenticates with secret '{key.Secret}', the "
            + $"{key.Variable} it used before. Set its auth to say so and silence this note.");

    public static StartupFinding Switched(string field, string entity, string auth, LegacyCredentialKey key) =>
        Advisory($"{field}.auth",
            $"{entity} now authenticates with secret '{auth}' instead of {key.Variable}: its auth was "
            + $"ignored until now and {key.Variable} is set.");

    public static StartupFinding EmptySecret(string field, string entity, string auth) =>
        Advisory($"{field}.auth-value",
            $"{entity} authenticates with secret '{auth}', which resolves empty; its first call will "
            + "fail until the variable behind the secret is set.");

    public static StartupFinding CatalogAdded(LegacyCredentialKey key) =>
        Advisory($"secrets.{key.Secret}",
            $"The secrets: catalog has no '{key.Secret}'; it was added as ${{{key.Variable}}} for the "
            + "entries that authenticate with it. Declare it in secrets: to silence this note.");

    public static StartupFinding FieldFilled(string field, string entity, string key, string source) =>
        Advisory($"{field}.{key}",
            $"{entity} declares no {key}; it takes {source}, as it did before. Set {key} on the "
            + "entry to silence this note.");

    private static StartupFinding Advisory(string field, string reason) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory, reason, Field: field);
}
