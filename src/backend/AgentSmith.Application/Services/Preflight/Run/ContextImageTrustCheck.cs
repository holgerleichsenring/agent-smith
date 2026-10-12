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
/// The image chain consults a context's image only when the project names none —
/// sandbox.toolchain_image, then sandbox.images for the language — so an image the operator
/// already overrode is not reported. A sandbox with no recorded image runs on the host and
/// runs no image at all, so it is not reported either.
/// </para>
/// <para>
/// A REPORT, not a gate: the chain falls through to the language catalog, whose image may
/// carry the toolchain. "Probably wrong" earns a warning, as in <see cref="RegistryCredentialCheck"/>.
/// </para>
/// </summary>
public sealed class ContextImageTrustCheck(ContextImageBoundary boundary) : IRunPreflightCheck
{
    public string Name => "context-image-trust";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var refused = Refused(pipeline);
        if (refused.Count == 0)
            return Task.FromResult(RunPreflightFinding.Pass(
                Name, "every context image the sandboxes consult is inside the registry boundary"));
        var contexts = string.Join("; ", refused.Select(r => $"context '{r.Context}': {r.Refusal.Reason}"));
        return Task.FromResult(new RunPreflightFinding(
            Name, RunPreflightVerdict.Warn, contexts,
            string.Join("; ", refused.Select(r => r.Refusal.Lever).Distinct(StringComparer.Ordinal))));
    }

    private List<(string Context, ContextImageRefusal Refusal)> Refused(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox) || bySandbox is null
            || !pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.SandboxImages, out var images) || images is null)
            return [];
        pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project);
        return bySandbox
            .Where(entry => images.ContainsKey(entry.Key))
            .SelectMany(entry => entry.Value)
            .Where(d => !OperatorNamesImage(project, d.Language))
            .Select(d => (d.ContextName, Refusal: boundary.Judge(d.ToolchainImage)))
            .Where(r => r.Refusal is not null)
            .Select(r => (r.ContextName, r.Refusal!))
            .ToList();
    }

    // The two links that come before the context's own image in SandboxImageChain.Resolve.
    private static bool OperatorNamesImage(ResolvedProject? project, string? language)
    {
        var sandbox = project?.Sandbox;
        if (sandbox is null) return false;
        if (!string.IsNullOrEmpty(sandbox.ToolchainImage)) return true;
        return !string.IsNullOrEmpty(language) && sandbox.Images is { } named && named.Any(kv =>
            string.Equals(kv.Key, language, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(kv.Value));
    }
}
