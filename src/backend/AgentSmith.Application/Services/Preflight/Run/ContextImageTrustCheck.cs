using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfac: names every context whose stack.image the registry boundary refused, on
/// the run, before the run spends half an hour in the image it got instead.
/// <para>
/// 2026-10-12-50bda: a GATE, no longer a report. The image the sandbox would be started in is a
/// substitute nobody chose — the catalog's guess or the generic image. Whether the context's image
/// was consulted at all is the chain's own decision, so an image the operator already overrode is
/// never reported, and a refused image the chain lands on anyway is no substitution.
/// </para>
/// <para>
/// 2026-11-16-f3fba: judged before any sandbox, over the contexts the sandboxes will be started for
/// (<see cref="RunContextInventory"/>). A backend that spawns no containers runs no image, so nothing
/// is judged there. The refusal carries the first image as an <c>EndDetail</c>.
/// </para>
/// </summary>
public sealed class ContextImageTrustCheck(
    ContextImageBoundary boundary, SandboxImageChain chain, RunContextInventory inventory,
    SandboxContainerRuntime runtime) : IRunPreflightCheck
{
    private const string ContextLever =
        "or declare a stack.image inside the boundary that carries the context's whole toolchain";

    /// <summary>2026-10-16-86e6a: the check's name, which the preflight gate classifies a refusal by.</summary>
    public const string CheckName = "context-image-trust";

    public string Name => CheckName;

    public bool NeedsSandbox => false;

    public async Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (!runtime.SpawnsContainers)
            return RunPreflightFinding.Pass(Name, "the sandbox backend runs on the host and starts no image");
        var refused = Refused(pipeline, await inventory.ReadAsync(pipeline, cancellationToken));
        if (refused.Count == 0)
            return RunPreflightFinding.Pass(
                Name, "every context image the sandboxes consult is inside the registry boundary");
        var contexts = string.Join("; ", refused.Select(r =>
            $"context '{r.Context}': {r.Refusal.Reason}, and the run will not substitute another"));
        var first = refused[0];
        return RunPreflightFinding.Fail(
            Name, contexts,
            string.Join("; ", refused.Select(r => r.Refusal.Lever).Distinct(StringComparer.Ordinal))
            + "; " + ContextLever) with
        {
            Kind = RunEndKinds.ImageUntrusted,
            Detail = boundary.Detail(first.Refusal.Image, first.Context, first.Repo),
        };
    }

    private List<(string Repo, string Context, ContextImageRefusal Refusal)> Refused(
        PipelineContext pipeline, IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> byRepo)
    {
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) && p is not null
            ? p : new ResolvedProject();
        return byRepo
            .SelectMany(entry => entry.Value.Select(d => (Repo: entry.Key, Discovery: d)))
            .Select(r => (r.Repo, r.Discovery.ContextName,
                Decision: chain.Decide(project, r.Discovery.Language, r.Discovery.ToolchainImage)))
            .Where(r => r.Decision.SubstitutesTheDeclaredImage)
            .Select(r => (r.Repo, r.ContextName, Refusal: boundary.Judge(r.Decision.RefusedContextImage)))
            .Where(r => r.Refusal is not null)
            .Select(r => (r.Repo, r.ContextName, r.Refusal!))
            .ToList();
    }
}
