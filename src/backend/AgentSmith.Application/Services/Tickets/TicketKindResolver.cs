using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tickets;

/// <summary>
/// 2026-09-18-b4f0: turns the ROLE a call site is filing into the tracker's own work-item
/// kind, on the filing side, before the create. The role never crosses the provider
/// interface — the port speaks tracker vocabulary, and two of its four implementations would
/// accept a filing-domain role only to ignore it.
/// <para>
/// The map's keys are FREE TEXT: no capability field can declare a map's legal keys today, so
/// a mistyped role saves cleanly, renders cleanly and would otherwise file the literal in
/// silence. A wrong role key is less observable than a wrong lifecycle one — a wrong lifecycle
/// key falls back to label mode and shows up in the tracker within a run, while a wrong role
/// key files at the wrong hierarchy level and surfaces only as a failed parent link — so every
/// key that is not a role is WARNED about, the way an unknown lifecycle key already is.
/// </para>
/// </summary>
public sealed class TicketKindResolver(ILogger<TicketKindResolver> logger)
{
    /// <summary>
    /// The configured kind for <paramref name="role"/>, or null when the tracker configures
    /// none — which is what every installation that has chosen nothing returns, and what makes
    /// the provider send the literal it sent before this key existed.
    /// </summary>
    public string? For(ResolvedProject project, TicketFilingRole role)
    {
        ArgumentNullException.ThrowIfNull(project);
        var tracker = project.Tracker;
        if (tracker.WorkItemKinds.Count == 0)
        {
            // 2026-09-24-f962: said out loud, because the consequence is invisible until much
            // later: an unconfigured tracker files the provider's literal, and a lifecycle status
            // that this literal's type does not have blocks the ticket for good at the run's end.
            logger.LogInformation(
                "Tracker '{Tracker}' configures no work_item_kinds; filing {Role} as the "
                + "provider's default type", tracker.Name, role);
            return null;
        }

        string? kind = null;
        foreach (var (key, configured) in tracker.WorkItemKinds)
        {
            if (!Enum.TryParse<TicketFilingRole>(key, ignoreCase: true, out var configuredRole))
                logger.LogWarning(
                    "Tracker '{Tracker}' work_item_kinds: unknown filing role '{Key}' ignored "
                    + "(known: {Roles})", tracker.Name, key, string.Join(", ", KnownRoles));
            else if (configuredRole == role)
                kind = configured;
        }
        logger.LogInformation(
            "Tracker '{Tracker}' files {Role} as '{Kind}'", tracker.Name, role,
            kind ?? "the provider's default type");
        return kind;
    }

    private static readonly IReadOnlyList<string> KnownRoles =
        [.. Enum.GetNames<TicketFilingRole>().Select(n => n.ToLowerInvariant())];
}
