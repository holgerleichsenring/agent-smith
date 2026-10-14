using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcem: shows each stored requires edge of a filed list on the tracker — a person's view, written once and
/// never read back; the requirements table is what holds a ticket. One <see cref="Deadline"/> bounds the whole pass on a
/// token only the deadline cancels, and the first tracker that does not answer ends it: the edges never reached are
/// named in one note, so a hung tracker costs the filing a minute, not a client timeout per edge.
/// </summary>
public sealed class TicketGraphLinks(ITicketProviderFactory factory, TimeProvider time, ILogger<TicketGraphLinks> logger)
{
    /// <summary>Default pending operator ratification.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<string>> LinkAsync(ResolvedProject project, IReadOnlyList<TicketRequirementEdge> stored)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (stored.Count == 0) return [];
        var notes = new List<string>();
        using var deadline = new CancellationTokenSource(Deadline, time);
        var at = 0;
        try
        {
            var link = factory.CreateRequiresLink(project.Tracker);
            for (; at < stored.Count; at++)
            {
                var edge = stored[at];
                var result = await link.LinkAsync(new TicketId(edge.TicketId), new TicketId(edge.RequiredTicketId), deadline.Token)
                    .WaitAsync(deadline.Token);
                if (result.Unreachable) break;
                if (!result.Linked)
                    notes.Add($"The tracker does not show that {Key(project, edge.TicketId)} requires {Key(project, edge.RequiredTicketId)} "
                        + $"({result.Reason}); the requirement is stored and holds {Key(project, edge.TicketId)} anyway.");
            }
        }
        // A link pass that throws would cost the filing's report; whatever it did not reach is named instead.
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Linking the requires of a filed list on {Tracker} stopped at edge {At} of {Count}", project.Tracker.Name, at + 1, stored.Count);
        }
        if (at < stored.Count)
            notes.Add("Not linked on the tracker (the tracker did not answer): " + string.Join(", ", stored.Skip(at)
                .Select(e => $"{Key(project, e.TicketId)} requires {Key(project, e.RequiredTicketId)}"))
                + "; the requirements are stored and hold those tickets anyway.");
        return notes;
    }

    private static string Key(ResolvedProject project, string id) => FiledTicketKey.Of(project.Tracker.Type, id);
}
