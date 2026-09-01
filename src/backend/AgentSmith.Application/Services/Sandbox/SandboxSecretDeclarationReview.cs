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
    public static IReadOnlyList<string> Problems(SandboxSecrets? secrets) =>
        secrets is null ? [] : [.. EnvProblems(secrets.Env), .. FileProblems(secrets.Files)];

    private static IEnumerable<string> EnvProblems(Dictionary<string, string>? env) =>
        (env ?? []).SelectMany(entry => EnvProblem(entry.Key, entry.Value));

    private static IEnumerable<string> EnvProblem(string name, string? value)
    {
        if (!EnvNameRegex().IsMatch(name ?? string.Empty))
            yield return $"sandbox.secrets.env['{name}'] is not a usable environment variable name";
        if (!SandboxSecretReference.TryParse(value, out _))
            yield return $"sandbox.secrets.env['{name}'] is not a 'secretName:key' reference";
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
