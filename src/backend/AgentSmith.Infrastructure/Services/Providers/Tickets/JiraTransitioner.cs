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
    public async Task TransitionAsync(
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
            return;
        }

        await http.SendAsync(HttpMethod.Post, url,
            new { transition = new { id = transitionId } }, cancellationToken);
    }

    private static string? FindTransitionId(
        JsonElement root, string primaryName, string? fallbackName)
    {
        if (!root.TryGetProperty("transitions", out var transitions)) return null;
        foreach (var transition in transitions.EnumerateArray())
        {
            var name = transition.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (name is null) continue;
            var matches = name.Contains(primaryName, StringComparison.OrdinalIgnoreCase)
                || (fallbackName is not null
                    && name.Contains(fallbackName, StringComparison.OrdinalIgnoreCase));
            if (matches)
                return transition.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        }
        return null;
    }
}
