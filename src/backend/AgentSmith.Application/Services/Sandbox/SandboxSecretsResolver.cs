using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <inheritdoc />
public sealed class SandboxSecretsResolver : ISandboxSecretsResolver
{
    public ResolvedSandboxSecrets Resolve(SandboxConfig? sandbox)
    {
        var secrets = sandbox?.Secrets;
        if (secrets is null) return ResolvedSandboxSecrets.Empty;

        var env = (secrets.Env ?? []).Select(ToEnvBinding).ToList();
        var files = (secrets.Files ?? []).Select(ToFileMount).ToList();
        return new ResolvedSandboxSecrets(env, files);
    }

    private static SecretEnvBinding ToEnvBinding(KeyValuePair<string, string> entry) =>
        new(entry.Key, ParseRef(entry.Value, entry.Key));

    private static SecretFileMount ToFileMount(SandboxSecretFile file) =>
        new(file.Mount, new SecretRef(file.Secret, file.Key));

    // The env reference is "secretName:key" — a single colon splits the two.
    // Anything else is an operator typo we refuse loudly rather than guess around.
    private static SecretRef ParseRef(string value, string envName)
    {
        var parts = (value ?? string.Empty).Split(':', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            throw new ArgumentException(
                $"sandbox.secrets.env['{envName}'] must be 'secretName:key' but was '{value}'.",
                nameof(value));
        return new SecretRef(parts[0], parts[1]);
    }
}
