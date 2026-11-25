namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0272: operator-declared credentials injected into the k8s sandbox pod. The
/// VALUES live in operator-created Kubernetes Secrets — agent-smith only carries
/// the references into the pod spec (secretKeyRef for env, a Secret volume for
/// files), so a secret value never appears in a Step/Redis message, in
/// context.yaml, or in the LLM's view. The auth COMMAND that consumes these (e.g.
/// <c>sf org login jwt</c>) lives in context.yaml <c>prerequisites</c> as an
/// <c>sh -c "…"</c> line referencing the injected env/file by name.
/// </summary>
public sealed class SandboxSecrets
{
    /// <summary>
    /// Environment variables sourced from a Kubernetes Secret. Each value is a
    /// <c>secretName:key</c> reference (e.g. <c>SF_CLIENT_ID: "sf-creds:client-id"</c>);
    /// a value without a single <c>:</c> separator is rejected at resolve time.
    /// </summary>
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>
    /// 2026-11-25-9992d: variables only one repository's commands receive, keyed by repository name, each value a
    /// <c>secretName:key</c> reference like <see cref="Env"/>. <see cref="Env"/> stays project-wide: every command of
    /// every repository receives it.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? RepoEnv { get; set; }

    /// <summary>Secret keys mounted as read-only files (e.g. a JWT private key).</summary>
    public List<SandboxSecretFile>? Files { get; set; }

    /// <summary>Every declaration: project-wide variables, files and each repository's variables. A method, so no
    /// serializer writes it back into the configuration.</summary>
    public int DeclaredCount() =>
        (Env?.Count ?? 0) + (Files?.Count ?? 0) + (RepoEnv?.Sum(repo => repo.Value?.Count ?? 0) ?? 0);
}
