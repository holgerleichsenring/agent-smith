using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-08-26-31e5: writes the index line for a finished phase into the <c>state.done</c> of
/// the context the change was made in.
/// <para>
/// The SANDBOX routes it, not the spec's <c>applies_to</c>: that key is in no schema, is set
/// by nothing in the product, is absent from half the specs here, and where present names an
/// AREA ("backend") rather than a context. The change was made in a sandbox, and
/// <see cref="SandboxTargets"/> already resolves that sandbox's context.
/// </para>
/// </summary>
public sealed class PhaseIndexWriter(
    ISandboxFileReaderFactory readerFactory,
    IContextYamlStateDoneCodec codec,
    SandboxTargets sandboxTargets,
    ILogger<PhaseIndexWriter> logger)
{
    /// <summary>True when the line reached the context — a pointer without one is the defect.</summary>
    public async Task<bool> WriteAsync(
        PipelineContext pipeline, ISandbox sandbox, string? sandboxKey,
        string repoLocalPath, string phaseId, string line, CancellationToken ct)
    {
        var discovery = Discovery(pipeline, sandbox, sandboxKey);
        if (discovery is null)
        {
            logger.LogWarning(
                "PhaseIndexWriter: no discovered context for sandbox '{Key}' — no index line",
                sandboxKey ?? "(single)");
            return false;
        }

        var path = Path.Combine(
            repoLocalPath, ProjectMetaPaths.Contexts, discovery.ContextName,
            ProjectMetaPaths.ContextYamlFile);
        var reader = readerFactory.Create(sandbox);
        // A context that was discovered but has no file on this sandbox is seeded from the
        // discovery's own workdir — the alternative is a pointer nothing indexes.
        var existing = await reader.TryReadAsync(path, ct)
            ?? $"meta:\n  workdir: \"{discovery.Workdir}\"\n";

        var upserted = codec.Upsert(existing, phaseId, line);
        if (upserted.Yaml is null)
        {
            logger.LogWarning(
                "PhaseIndexWriter: {Path} left untouched — {Reason}", path, upserted.ParseError);
            return false;
        }
        await reader.WriteAsync(path, upserted.Yaml, ct);
        logger.LogInformation("Phase index line written to {Path}", path);
        return true;
    }

    private RemoteContextDiscovery? Discovery(
        PipelineContext pipeline, ISandbox sandbox, string? sandboxKey)
    {
        if (!sandboxTargets.TryResolve(pipeline, out var sandboxes, out var discoveries))
            return null;
        var key = sandboxKey
            ?? sandboxes.FirstOrDefault(pair => ReferenceEquals(pair.Value, sandbox)).Key;
        if (key is not null && discoveries.TryGetValue(key, out var discovery)) return discovery;
        return discoveries.Count == 1 ? discoveries.Values.First() : null;
    }
}
