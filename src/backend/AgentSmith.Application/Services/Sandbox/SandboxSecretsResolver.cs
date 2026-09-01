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

    // The env reference is "secretName:key" — a single colon splits the two. Anything else
    // is an operator typo we refuse loudly rather than guess around; the reading itself
    // lives in SandboxSecretReference so the preflight check and this call site cannot
    // disagree about which reference is well-formed. 2026-08-28-b630: the message names the
    // VARIABLE and not what was written into it — a field an operator mistyped may hold the
    // value they meant to reference.
    private static SecretRef ParseRef(string value, string envName)
    {
        if (!SandboxSecretReference.TryParse(value, out var parsed))
            throw new ArgumentException(
                $"sandbox.secrets.env['{envName}'] must be 'secretName:key'.", nameof(value));
        return parsed!;
    }
}
