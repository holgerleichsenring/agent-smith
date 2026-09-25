using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-25-3c7aa: whether an approved specification exists for a ticket, asked where an
/// incoming envelope is built so the answer rides on the envelope instead of on a label.
/// <para>
/// TWO DOORS, BECAUSE THE TWO ROUTED PATHS KNOW DIFFERENT THINGS. A poll runs for ONE tracker
/// connection and names it. A webhook route is per PLATFORM — two Jira connections share one
/// endpoint — so it sweeps every configured connection of that type and takes the first answer,
/// the same shape the spawn dispatcher already uses for the same reason.
/// </para>
/// <para>
/// It never throws and never blocks a ticket: a store this process cannot reach answers NO, and
/// the two labels that also bind are read beside it. The failure mode is therefore exactly
/// today's behaviour rather than a ticket that stops routing.
/// </para>
/// </summary>
public sealed class ApprovedRecordProbe(ISpecApprovalStore store, ILogger<ApprovedRecordProbe> logger)
{
    /// <summary>The poll's door: one named tracker connection.</summary>
    public async Task<bool> ExistsAsync(
        string tracker, string? platform, string? ticketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(ticketId)) return false;
        var key = SpecSetKey.For(platform!, ticketId!);
        try
        {
            return await store.GetAsync(tracker ?? string.Empty, key.Value, cancellationToken) is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex,
                "The approval record for {Key} on tracker {Tracker} could not be read; "
                + "this ticket routes on its labels alone", key.Value, tracker);
            return false;
        }
    }

    /// <summary>The webhook's door: every configured connection of the payload's platform.</summary>
    public async Task<bool> ExistsForPlatformAsync(
        AgentSmithConfig config, string? platform, string? ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(ticketId)) return false;
        foreach (var tracker in config.Trackers.Values.Where(t => IsPlatform(t, platform!)))
            if (await ExistsAsync(tracker.Name, platform, ticketId, cancellationToken)) return true;
        return false;
    }

    /// <summary>The envelope's platform word as every builder spells it — the enum name, lowered.</summary>
    private static bool IsPlatform(TrackerConnection tracker, string platform) =>
        string.Equals(tracker.Type.ToString(), platform, StringComparison.OrdinalIgnoreCase);
}
