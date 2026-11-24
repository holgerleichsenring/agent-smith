using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the one serialisation of a PoC's runs. The run id is minted first; the repository then claims the
/// PoC's slot and writes the queued row in one transaction; only after the commit is the run dispatched. A lost claim
/// names the run in progress. Every PoC run of the later slices passes here; none adds a lock of its own.
/// </summary>
internal sealed class PocRunGate(
    PocRunGateRepository gate, QueuedRunDispatch dispatch, TimeProvider clock, ILogger<PocRunGate> logger)
{
    /// <summary>The run of <paramref name="poc"/> still under way, or null.</summary>
    public async Task<string?> LiveRunAsync(Poc poc, CancellationToken ct) =>
        poc.LastRunId is { } runId && await gate.IsLiveAsync(runId, RunLivenessReaper.StaleAfter, ct) ? runId : null;

    /// <summary>Claims <paramref name="poc"/>'s slot for a poc-deploy run of <paramref name="revision"/> and queues it;
    /// the run id, or null with the run that holds the slot.</summary>
    public async Task<(string? RunId, string? HeldBy)> QueueDeployAsync(
        Poc poc, PocRevision revision, string actor, CancellationToken ct, string? handover = null,
        IReadOnlyDictionary<string, object>? extraSeeds = null)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(revision);
        var (runId, heldBy) = await ClaimAsync(poc, PipelinePresets.PocDeployName,
            $"starting — {(extraSeeds is null ? "checking" : "overruling")} revision {revision.Number} of {poc.Name}", ct, revision.Id, handover);
        if (runId is null) return (null, heldBy);
        await dispatch.DispatchAsync(new PipelineRequest(poc.Project, PipelinePresets.PocDeployName, TicketId: null, Headless: true,
            RunId: runId, Context: With(Seeds(poc.Id, revision.Number, actor), extraSeeds)), ct);
        logger.LogInformation("poc-deploy of PoC {PocId} revision {Revision} queued (run {RunId})", poc.Id, revision.Number, runId);
        return (runId, null);
    }

    /// <summary>2026-11-23-933dg: claims <paramref name="poc"/>'s slot for a run of <paramref name="pipeline"/> — a minted
    /// run id with its queued row, the caller then runs it (IActRun with that run id) or dispatches it; the run id, or
    /// null with the run that holds the slot.</summary>
    public async Task<(string? RunId, string? HeldBy)> ClaimAsync(
        Poc poc, string pipeline, string summary, CancellationToken ct, long? revisionId = null, string? handover = null)
    {
        ArgumentNullException.ThrowIfNull(poc);
        var runId = RunIdGenerator.Generate(clock.GetUtcNow());
        var claim = await gate.ClaimAsync(new PocClaimRequest(poc.Id, runId, poc.Project, pipeline, [], summary,
            RunLivenessReaper.StaleAfter, revisionId, handover), ct);
        return claim.Won ? (runId, null) : (null, claim.LiveRunId);
    }

    // 2026-11-23-933dc: an overrule run carries its reason and the lifted ids beside the common seeds.
    private static Dictionary<string, object> With(Dictionary<string, object> seeds, IReadOnlyDictionary<string, object>? extra)
    {
        foreach (var (key, value) in extra ?? new Dictionary<string, object>()) seeds[key] = value;
        return seeds;
    }

    /// <summary>The run's seeds — strings only, so the request survives the queue.</summary>
    public static Dictionary<string, object> Seeds(long pocId, int revision, string actor) => new()
    {
        [ContextKeys.PocId] = pocId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [ContextKeys.PocRevision] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [ContextKeys.PocActor] = actor,
    };
}
