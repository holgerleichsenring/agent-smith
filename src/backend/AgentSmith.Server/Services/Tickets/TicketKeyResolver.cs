using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket key a bound conversation and a ticket nudge name a ticket by —
/// TicketKey.For(platform, ticket id) with the platform the run row stamped (the project's tracker
/// type lowercased), so a removed project's ticket still matches its stored conversation. Live
/// configuration is the fallback only.
/// </summary>
public sealed class TicketKeyResolver(AgentSmithConfig config)
{
    public string? For(string project, string? platform, string ticketId)
    {
        var provider = !string.IsNullOrEmpty(platform) ? platform : TrackerTypeOf(project);
        return provider is null ? null : TicketKey.For(provider, ticketId).Value;
    }

    private string? TrackerTypeOf(string project) =>
        config.Projects.FirstOrDefault(p => ConfigNames.AreSame(p.Key, project)).Value is { } resolved
            ? resolved.Tracker.Type.ToString().ToLowerInvariant()
            : null;
}
