using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-08-28-b630: a credential the project declares is well-formed, unambiguous and
/// distinct in its mounts — decided from the declaration alone, so it costs no cluster call
/// and cannot leak a value.
/// <para>
/// The typo is the common defect and it is invisible until the auth command inside the
/// sandbox fails an hour in, reading there as a broken build rather than as a colon nobody
/// typed. Reading the cluster to check further is refused on three independent grounds:
/// the deployment's role grants no secrets so the call would 403, the API has no
/// existence-without-content read so granting it means granting every value in the
/// namespace, and a preflight check that throws is downgraded to a warning — the check
/// would report everything fine exactly in the deployments where it cannot look.
/// </para>
/// </summary>
public sealed class DeclaredSecretCheck : IRunPreflightCheck
{
    private const string Lever =
        "write each credential as sandbox.secrets.env.<NAME>: '<secretName>:<key>' and give "
        + "every sandbox.secrets.files entry its own absolute mount, secret and key — the "
        + "values stay in the operator's Kubernetes Secrets and are never read from here";

    public string Name => "declared-secrets";

    public Task<RunPreflightFinding> RunAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var declared = Declared(pipeline);
        var count = (declared?.Env?.Count ?? 0) + (declared?.Files?.Count ?? 0);
        if (count == 0)
            return Task.FromResult(RunPreflightFinding.Pass(
                Name, "no credentials are declared — nothing to check"));

        var problems = SandboxSecretDeclarationReview.Problems(declared);
        return Task.FromResult(problems.Count == 0
            ? RunPreflightFinding.Pass(Name, $"{count} declared credential reference(s) well-formed")
            : RunPreflightFinding.Fail(Name, string.Join("; ", problems), Lever));
    }

    private static SandboxSecrets? Declared(PipelineContext pipeline) =>
        pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            ? project?.Sandbox?.Secrets
            : null;
}
