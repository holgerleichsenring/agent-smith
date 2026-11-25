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

        var env = (secrets.Env ?? []).Select(e => new SecretEnvBinding(e.Key, ParseRef(e.Value, $"env['{e.Key}']")))
            .ToList();
        var files = (secrets.Files ?? []).Select(ToFileMount).ToList();
        // 2026-11-25-9992d: a repository's own variables, read with the same reference rule.
        var repoEnv = (secrets.RepoEnv ?? [])
            .SelectMany(repo => (repo.Value ?? []).Select(e => new SecretEnvBinding(
                e.Key, ParseRef(e.Value, $"repo_env['{repo.Key}']['{e.Key}']"), repo.Key)))
            .ToList();
        return new ResolvedSandboxSecrets(env, files, repoEnv);
    }

    private static SecretFileMount ToFileMount(SandboxSecretFile file) =>
        new(file.Mount, new SecretRef(file.Secret, file.Key));

    // The env reference is "secretName:key" — a single colon splits the two. Anything else
    // is an operator typo we refuse loudly rather than guess around; the reading itself
    // lives in SandboxSecretReference so the preflight check and this call site cannot
    // disagree about which reference is well-formed. 2026-08-28-b630: the message names the
    // VARIABLE and not what was written into it — a field an operator mistyped may hold the
    // value they meant to reference.
    private static SecretRef ParseRef(string value, string field)
    {
        if (!SandboxSecretReference.TryParse(value, out var parsed))
            throw new ArgumentException(
                $"sandbox.secrets.{field} must be 'secretName:key'.", nameof(value));
        return parsed!;
    }
}
