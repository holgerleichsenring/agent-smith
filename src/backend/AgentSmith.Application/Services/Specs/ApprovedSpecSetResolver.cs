using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: the ONE answer to "what did this ticket's approval say", for every step
/// that asks. ScopeRepos is position 4 of the code preset and DeriveSpec position 14, and a
/// fallback resolved only at DeriveSpec would leave ScopeRepos classifying a thin ticket — with
/// the carrying repo scoped out, the next run finds no artifact at all.
/// <para>
/// Two routes, because one of them is not always there. The CARRY works for every launcher; the
/// STORE is the fallback for a process that has one, which is what repairs a request nobody
/// built a context for. Where both answer, the NEWER approval wins: a capacity-queue entry
/// freezes its context when the candidate is built and the pump launches it later, in the
/// server, so the carried copy can be the stale one.
/// </para>
/// <para>
/// A carried record is checked against the run's OWN key AND its own tracker connection. A
/// record for another ticket — or for a ticket of that number on a different tracker instance —
/// is a re-used queue row or a hand-built request; it is ignored, and the run then has no set,
/// so a filed ticket fails loudly rather than being worked to someone else's specification.
/// </para>
/// </summary>
public sealed class ApprovedSpecSetResolver(
    ISpecApprovalStore store,
    ILogger<ApprovedSpecSetResolver> logger)
{
    /// <summary>The approval that governs this run, or null when nobody approved this ticket.</summary>
    public async Task<SpecApprovalRecord?> ResolveAsync(
        PipelineContext pipeline, SpecSetKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var tracker = TrackerOf(pipeline);
        var winner = Newer(
            Carried(pipeline, key, tracker), await StoredAsync(tracker, key, cancellationToken));
        // Published back under the same key, so every later step reads the decision rather
        // than making it again — and a stale carried copy is replaced by the one that won.
        if (winner is not null)
            pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(winner));
        return winner;
    }

    /// <summary>The tracker connection this run's ticket lives on; empty on a run with none.</summary>
    private static string TrackerOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.TrackerConnection, out var name) && name is not null
            ? name : string.Empty;

    private SpecApprovalRecord? Carried(PipelineContext pipeline, SpecSetKey key, string tracker)
    {
        var record = SpecApprovalJson.Read(CarriedJson(pipeline));
        if (record is null) return null;
        if (string.Equals(record.Key, key.Value, StringComparison.Ordinal)
            && string.Equals(record.Tracker, tracker, StringComparison.OrdinalIgnoreCase))
            return record;
        logger.LogWarning(
            "The carried approved spec set is {Carried} on tracker '{CarriedTracker}' but this run's "
            + "ticket is {Key} on '{Tracker}' — it belongs to another ticket and is ignored",
            record.Key, record.Tracker, key.Value, tracker);
        return null;
    }

    // The record rides the request context as a plain string, but the Redis job queue's JSON
    // round-trip re-materializes request values as JsonElement (ResumePayload's precedent).
    private static string? CarriedJson(PipelineContext pipeline)
    {
        if (!pipeline.Has(ContextKeys.ApprovedSpecSet)) return null;
        if (pipeline.TryGet<string>(ContextKeys.ApprovedSpecSet, out var text) && text is not null)
            return text;
        return pipeline.TryGet<JsonElement>(ContextKeys.ApprovedSpecSet, out var element)
            && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private async Task<SpecApprovalRecord?> StoredAsync(
        string tracker, SpecSetKey key, CancellationToken ct)
    {
        try { return await store.GetAsync(tracker, key.Value, ct); }
        // A store this process cannot reach answers nothing; the carry is the primary route and
        // a filed ticket with neither still fails loudly at DeriveSpec.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reading the approved spec set for {Key} failed", key.Value);
            return null;
        }
    }

    private SpecApprovalRecord? Newer(SpecApprovalRecord? carried, SpecApprovalRecord? stored)
    {
        if (stored is null) return carried;
        if (carried is null) return stored;
        if (stored.Approval is not { } approval || !approval.IsNewerThan(carried.Approval)) return carried;
        logger.LogInformation(
            "The stored approval of {Key} is newer than the one the run carried — using the stored one",
            stored.Key);
        return stored;
    }
}
