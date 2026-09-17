using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: puts the approved record on a run's INITIAL context, for the paths that
/// build a request before any pipeline exists — the spawn funnel's two request shapes and the
/// reconciler's bare re-enqueue of an orphaned ticket.
/// <para>
/// The carry is the primary route because it works for every launcher: a funnel run executes in
/// the server, but a chat run is a spawned container and a CLI run is a process, and neither
/// composition swaps a store in. What the funnel reads here is what those processes get.
/// </para>
/// </summary>
public sealed class ApprovedSpecSetCarrier(
    ISpecApprovalStore store,
    ILogger<ApprovedSpecSetCarrier> logger)
{
    /// <summary>The record for this ticket as the wire string, or null when nobody approved it.</summary>
    /// <param name="tracker">The tracker CONNECTION's catalog name — the other half of the
    /// record's identity, because the spec key carries only the tracker's type.</param>
    public async Task<string?> JsonForAsync(
        string tracker, string? platform, string? ticketId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(ticketId)) return null;
        var key = SpecSetKey.For(platform!, ticketId!);
        try
        {
            var record = await store.GetAsync(tracker ?? string.Empty, key.Value, ct);
            if (record is null) return null;
            logger.LogInformation(
                "Carrying the approved spec set {Key} ({Phases} phase(s)) onto the run's initial context",
                record.Key, record.Set.Phases.Count);
            return SpecApprovalJson.Write(record);
        }
        // A store this process cannot reach carries nothing; the run then resolves its own.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reading the approved spec set for {Key} failed", key.Value);
            return null;
        }
    }

    /// <summary>The initial context a bare request needs, or null when there is nothing to carry.
    /// Takes the CONNECTION, which carries both halves of the record's identity.</summary>
    public async Task<Dictionary<string, object>?> ContextForAsync(
        TrackerConnection tracker, string? ticketId, CancellationToken ct) =>
        await JsonForAsync(
            tracker?.Name ?? string.Empty,
            tracker?.Type.ToString().ToLowerInvariant(), ticketId, ct) is { } json
            ? new Dictionary<string, object> { [ContextKeys.ApprovedSpecSet] = json }
            : null;
}
