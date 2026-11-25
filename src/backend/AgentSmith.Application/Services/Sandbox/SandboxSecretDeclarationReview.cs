using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-28-b630: everything wrong with a declared credential that is decidable from the
/// DECLARATION, reported by name and without a cluster call.
/// <para>
/// The values live in operator-created Kubernetes Secrets and are deliberately unreadable
/// from here — the deployment's role grants jobs, pods and quotas and no secrets, and the
/// API has no existence-without-content read, so asking would mean granting the orchestrator
/// every value in the namespace. What is knowable without the cluster is the shape: a blank
/// side, a missing separator, an env name no shell can carry and two files claiming one mount
/// are all decidable from the resolved project, and they are most of the defects.
/// </para>
/// </summary>
public static partial class SandboxSecretDeclarationReview
{
    /// <param name="repositories">2026-11-25-9992d: the project's repository names; a repo_env key naming none of them
    /// is a problem. Matched Ordinal, as the sandbox's scope is (RepoSandbox stamps the RepoConnection name). Null skips
    /// the check.</param>
    public static IReadOnlyList<string> Problems(SandboxSecrets? secrets, IReadOnlyCollection<string>? repositories = null) =>
        secrets is null
            ? []
            : [.. EnvProblems("env", secrets.Env), .. RepoEnvProblems(secrets.RepoEnv),
                .. UnknownRepositories(secrets.RepoEnv, repositories), .. FileProblems(secrets.Files)];

    private static IEnumerable<string> UnknownRepositories(
        Dictionary<string, Dictionary<string, string>>? repoEnv, IReadOnlyCollection<string>? repositories) =>
        repositories is null
            ? []
            : (repoEnv ?? []).Keys
                .Where(repo => !string.IsNullOrWhiteSpace(repo) && !repositories.Contains(repo, StringComparer.Ordinal))
                .Select(repo => $"sandbox.secrets.repo_env['{repo}'] names no repository of the project "
                    + $"(exact names: {string.Join(", ", repositories.Order(StringComparer.Ordinal))})");

    private static IEnumerable<string> EnvProblems(string field, Dictionary<string, string>? env) =>
        (env ?? []).SelectMany(entry => EnvProblem(field, entry.Key, entry.Value));

    // 2026-11-25-9992d: a repository's own variables follow the env rules, under a repository that has a name.
    private static IEnumerable<string> RepoEnvProblems(Dictionary<string, Dictionary<string, string>>? repoEnv) =>
        (repoEnv ?? []).SelectMany(repo => string.IsNullOrWhiteSpace(repo.Key)
            ? ["sandbox.secrets.repo_env has an entry that names no repository"]
            : EnvProblems($"repo_env['{repo.Key}']", repo.Value));

    private static IEnumerable<string> EnvProblem(string field, string name, string? value)
    {
        if (!EnvNameRegex().IsMatch(name ?? string.Empty))
            yield return $"sandbox.secrets.{field}['{name}'] is not a usable environment variable name";
        if (!SandboxSecretReference.TryParse(value, out _))
            yield return $"sandbox.secrets.{field}['{name}'] is not a 'secretName:key' reference";
    }

    private static IEnumerable<string> FileProblems(List<SandboxSecretFile>? files)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files ?? [])
        {
            foreach (var problem in FileProblem(file)) yield return problem;
            if (!claimed.Add(file.Mount ?? string.Empty))
                yield return $"sandbox.secrets.files claims the mount '{file.Mount}' twice — "
                    + "two files cannot occupy one path";
        }
    }

    private static IEnumerable<string> FileProblem(SandboxSecretFile file)
    {
        if (!IsUsableMount(file.Mount))
            yield return $"sandbox.secrets.files mount '{file.Mount}' is not an absolute quote-free path";
        if (string.IsNullOrWhiteSpace(file.Secret) || string.IsNullOrWhiteSpace(file.Key))
            yield return $"sandbox.secrets.files entry for mount '{file.Mount}' names no secret and key";
    }

    // A mount is embedded verbatim in the pod spec and in the in-sandbox presence probe,
    // so a quote in it is refused here rather than escaped in three places later.
    private static bool IsUsableMount(string? mount) =>
        !string.IsNullOrWhiteSpace(mount) && mount.StartsWith('/') && !mount.Contains('\'');

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvNameRegex();
}
