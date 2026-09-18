using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: Jira's workflow transition flow is GET /transitions -> find
/// matching id -> POST /transitions. Substring match by name with an
/// optional fallback (e.g. status name first, "Close" transition name
/// second when closing). The transitions path comes from the operator-
/// overridable <see cref="JiraEndpoints"/>.
/// </summary>
internal sealed class JiraTransitioner(
    TicketProviderHttpClient http, string baseUrl, JiraEndpoints endpoints, ILogger logger)
{
    /// <summary>
    /// Posts the workflow transition whose target status matches <paramref name="primaryName"/>
    /// (or <paramref name="fallbackName"/>). Returns true when a matching transition was found
    /// and posted; false when none matched (the ticket stays in its current status). Callers that
    /// need a guaranteed record (e.g. lifecycle tracking) fall back to labels on a false result.
    /// </summary>
    public async Task<bool> TransitionAsync(
        TicketId ticketId, string primaryName, string? fallbackName,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}{endpoints.TransitionsFor(ticketId.Value)}";
        using var doc = await http.SendForJsonAsync(HttpMethod.Get, url, null, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Jira returned 404 fetching transitions for {ticketId.Value}");

        var transitionId = FindTransitionId(doc.RootElement, primaryName, fallbackName);
        if (transitionId is null)
        {
            logger.LogWarning(
                "No transition matching '{StatusName}' found for ticket {TicketId}. " +
                "The ticket will remain in its current state.", primaryName, ticketId.Value);
            return false;
        }

        await http.SendAsync(HttpMethod.Post, url,
            new { transition = new { id = transitionId } }, cancellationToken);
        return true;
    }

    /// <summary>
    /// 2026-09-17-042eg: the NAME match is tried first and unchanged — every transition it found
    /// before is still the one taken, which is what close, finalize and retry have always relied
    /// on. Only when no name matches is the transition whose target status IS the wanted status
    /// taken: a workflow that names its transitions for the act ("Start work") rather than for the
    /// destination ("In Progress") was otherwise unreachable, and a filed ticket could not be
    /// moved into a trigger status the operator had configured perfectly well.
    /// </summary>
    private static string? FindTransitionId(
        JsonElement root, string primaryName, string? fallbackName)
    {
        if (!root.TryGetProperty("transitions", out var transitions)) return null;
        // The FIRST name match decides, exactly as before — including the case where the tracker
        // sent it without an id, which answers null rather than falling through to a second pass
        // the name match was supposed to pre-empt.
        foreach (var transition in transitions.EnumerateArray())
            if (NameMatches(transition, primaryName, fallbackName)) return Id(transition);
        foreach (var transition in transitions.EnumerateArray())
            if (TargetIs(transition, primaryName)) return Id(transition);
        return null;
    }

    private static string? Id(JsonElement transition) =>
        transition.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

    private static bool NameMatches(JsonElement transition, string primaryName, string? fallbackName)
    {
        var name = transition.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        return name is not null
            && (name.Contains(primaryName, StringComparison.OrdinalIgnoreCase)
                || (fallbackName is not null
                    && name.Contains(fallbackName, StringComparison.OrdinalIgnoreCase)));
    }

    // Exact, never a substring: "To Do" must not be satisfied by a transition landing in "To Do Later".
    private static bool TargetIs(JsonElement transition, string statusName) =>
        transition.TryGetProperty("to", out var to)
        && to.TryGetProperty("name", out var nameEl)
        && string.Equals(nameEl.GetString(), statusName, StringComparison.OrdinalIgnoreCase);
}
