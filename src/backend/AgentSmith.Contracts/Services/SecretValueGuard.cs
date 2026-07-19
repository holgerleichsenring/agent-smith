using System.Text.Json;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0349: the secret invariant re-asserted for the opaque doc store — a secret
/// entity's doc must hold an env-var REFERENCE (<c>${NAME}</c>), never a resolved
/// value. Cheap to lose when the column is free JSON, so it is an explicit
/// save-time guard, not an assumption.
/// </summary>
public static class SecretValueGuard
{
    public const string SecretType = "secret";

    public static void Validate(string type, string id, string doc)
    {
        if (!string.Equals(type, SecretType, StringComparison.Ordinal)) return;

        var value = ReadValue(doc);
        if (string.IsNullOrEmpty(value)) return;
        if (IsEnvReference(value)) return;

        throw new ConfigurationException(
            $"Secret '{id}' carries a raw value; secrets store the env-var NAME only " +
            "(e.g. \"${GITHUB_TOKEN}\"), never a resolved value.");
    }

    private static bool IsEnvReference(string value) =>
        value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}');

    private static string? ReadValue(string doc)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(doc);
        }
        catch (JsonException)
        {
            // A non-string secret doc is malformed for this type; treat as a raw value.
            return doc;
        }
    }
}
