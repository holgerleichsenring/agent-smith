using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the checkouts a declared consumer's call sites can be read from.
/// <para>
/// <see cref="For"/> answers null when ANY declared consumer has no checkout in this run.
/// Reading the ones that happen to be present would compute the difference over a subset
/// the operator did not choose — which is exactly the shape of answer this spec exists to
/// refuse — so the run reports the difference as not computed instead.
/// </para>
/// </summary>
public sealed record SurfaceCheckouts(
    IReadOnlyDictionary<string, ISandbox> Sandboxes,
    string DefaultKey,
    IReadOnlyDictionary<string, string>? KeyToRepo,
    string RepoPath)
{
    public static SurfaceCheckouts? For(PipelineContext pipeline, IReadOnlyList<string> consumers)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(consumers);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes)
            || sandboxes is not { Count: > 0 })
            return null;

        var keyToRepo = pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, out var map) ? map : null;
        var keys = consumers.Select(c => KeyOf(c, sandboxes, keyToRepo)).ToList();
        if (keys.Any(k => k is null)) return null;

        // 2026-11-25-9992c: the tool host's root is the default checkout's own.
        return new SurfaceCheckouts(sandboxes, keys[0]!, keyToRepo, sandboxes[keys[0]!].WorkDirectory());
    }

    private static string? KeyOf(
        string consumer, IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo)
    {
        if (keyToRepo is not null)
            return keyToRepo.FirstOrDefault(e =>
                string.Equals(e.Value, consumer, StringComparison.OrdinalIgnoreCase)).Key;
        return sandboxes.Count == 1 ? sandboxes.Keys.First() : null;
    }

}
