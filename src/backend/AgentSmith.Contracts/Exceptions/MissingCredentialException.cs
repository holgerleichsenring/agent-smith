using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// 2026-10-02-5f89a: a git host or tracker call found no token behind its entity's auth secret.
/// The message names the entity and the secret, never a value, so it can be shown and logged
/// as it is; it is a <see cref="ConfigurationException"/> because the fix is in the configuration.
/// </summary>
public class MissingCredentialException : ConfigurationException
{
    public MissingCredentialException(string entity, string secret) : this(entity, secret, BuildMessage(entity, secret)) { }

    /// <summary>2026-11-25-9992h: a subtype's own sentence about the same entity and secret.</summary>
    protected MissingCredentialException(string entity, string secret, string message) : base(message)
    {
        Entity = entity;
        Secret = secret;
    }

    public string Entity { get; }
    public string Secret { get; }

    private static string BuildMessage(string entity, string secret) =>
        string.IsNullOrWhiteSpace(secret)
            ? $"{entity} names no auth secret; set 'auth' to an entry of the secrets: catalog."
            : $"{entity} authenticates with secret '{secret}', which holds no value; "
              + "define it in the secrets: catalog and set the variable it names.";
}
