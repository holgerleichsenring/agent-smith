using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// 2026-10-02-5f89a: a git host or tracker call found no token behind its entity's auth secret.
/// The message names the entity and the secret, never a value, so it can be shown and logged
/// as it is; it is a <see cref="ConfigurationException"/> because the fix is in the configuration.
/// </summary>
public sealed class MissingCredentialException(string entity, string secret)
    : ConfigurationException(BuildMessage(entity, secret))
{
    public string Entity { get; } = entity;
    public string Secret { get; } = secret;

    private static string BuildMessage(string entity, string secret) =>
        string.IsNullOrWhiteSpace(secret)
            ? $"{entity} names no auth secret; set 'auth' to an entry of the secrets: catalog."
            : $"{entity} authenticates with secret '{secret}', which holds no value; "
              + "define it in the secrets: catalog and set the variable it names.";
}
