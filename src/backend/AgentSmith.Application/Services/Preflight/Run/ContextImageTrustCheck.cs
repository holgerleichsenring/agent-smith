using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfac: names every context whose stack.image the registry boundary refused, on
/// the run, before the run spends half an hour in the image it got instead.
/// <para>
/// 2026-10-12-50bda: a GATE, no longer a report. The image the sandbox was started in is a
/// substitute nobody chose — the catalog's guess or the generic image — and "automatically
/// switching to a default image" cost a 25-minute run that failed on a missing build tool.
/// Whether the context's image was consulted at all is the chain's own decision, so an image
/// the operator already overrode is never reported, and a refused image the chain lands on
/// anyway is no substitution. A sandbox with no recorded image runs on the host and runs no
/// image at all, so it is not judged either.
/// </para>
/// </summary>
public sealed class ContextImageTrustCheck(
    ContextImageBoundary boundary, SandboxImageChain chain) : IRunPreflightCheck
{
    private const string ContextLever =
        "or declare a stack.image inside the boundary that carries the context's whole toolchain";

    public string Name => "context-image-trust";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var refused = Refused(pipeline);
        if (refused.Count == 0)
            return Task.FromResult(RunPreflightFinding.Pass(
                Name, "every context image the sandboxes consult is inside the registry boundary"));
        var contexts = string.Join("; ", refused.Select(r =>
            $"context '{r.Context}': {r.Refusal.Reason}, and the run will not substitute another"));
        return Task.FromResult(RunPreflightFinding.Fail(
            Name, contexts,
            string.Join("; ", refused.Select(r => r.Refusal.Lever).Distinct(StringComparer.Ordinal))
            + "; " + ContextLever));
    }

    private List<(string Context, ContextImageRefusal Refusal)> Refused(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox) || bySandbox is null
            || !pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.SandboxImages, out var images) || images is null)
            return [];
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) && p is not null
            ? p : new ResolvedProject();
        return bySandbox
            .Where(entry => images.ContainsKey(entry.Key))
            .SelectMany(entry => entry.Value)
            .Select(d => (d.ContextName, Decision: chain.Decide(project, d.Language, d.ToolchainImage)))
            .Where(r => r.Decision.SubstitutesTheDeclaredImage)
            .Select(r => (r.ContextName, Refusal: boundary.Judge(r.Decision.RefusedContextImage)))
            .Where(r => r.Refusal is not null)
            .Select(r => (r.ContextName, r.Refusal!))
            .ToList();
    }
}
