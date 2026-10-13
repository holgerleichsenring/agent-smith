using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-2123: a GitLab issue's reopens and closes (resource state events, state "reopened" / "closed") and the token's
/// user. Counts only when "opened" or "open" is a trigger status. Pages of 100 until a short one.
/// </summary>
internal sealed class GitLabStatusHistory(TicketProviderHttpClient http, string baseUrl, string projectPath)
    : ITicketStatusHistory, ITrackerSelf
{
    private const int PageSize = 100;
    private const int MaxPages = 20;

    public async Task<TrackerActor?> SelfAsync(CancellationToken cancellationToken)
    {
        using var me = await http.SendForJsonAsync(HttpMethod.Get, $"{baseUrl}/api/v4/user", null, cancellationToken);
        return me is null ? null : Actor(me.RootElement);
    }

    public async Task<TicketStatusMove?> NewestPersonMoveIntoAsync(
        TicketId ticketId, IReadOnlyCollection<string> statuses, TrackerActor? self, CancellationToken cancellationToken)
    {
        if (!statuses.Any(s => s.Equals("opened", StringComparison.OrdinalIgnoreCase) || s.Equals("open", StringComparison.OrdinalIgnoreCase)))
            return null;
        return (await MovesAsync(ticketId, "opened", cancellationToken)).Where(m => !m.Actor.Is(self)).MaxBy(m => m.At);
    }

    public async Task<TicketStatusMove?> NewestMoveIntoAsync(TicketId ticketId, string status, CancellationToken cancellationToken) =>
        (await MovesAsync(ticketId, status, cancellationToken)).MaxBy(m => m.At);

    // A state event "closed" moves into closed, "reopened" into opened; GitLab has no other issue state.
    private async Task<IReadOnlyList<TicketStatusMove>> MovesAsync(TicketId ticketId, string status, CancellationToken cancellationToken)
    {
        var into = status.ToLowerInvariant() switch { "closed" => "closed", "opened" or "open" => "reopened", _ => null };
        if (into is null) return [];
        var moves = new List<TicketStatusMove>();
        for (var page = 1; page <= MaxPages; page++)
        {
            using var doc = await http.SendForJsonOrThrowAsync(HttpMethod.Get,
                $"{baseUrl}/api/v4/projects/{projectPath}/issues/{ticketId.Value}/resource_state_events?per_page={PageSize}&page={page}", null, cancellationToken);
            foreach (var e in doc.RootElement.EnumerateArray())
                if (Str(e, "state") == into && e.TryGetProperty("user", out var u) && Actor(u) is { } actor
                    && DateTimeOffset.TryParse(Str(e, "created_at"), out var at))
                    moves.Add(new TicketStatusMove(actor, at, into == "closed" ? "closed" : "opened"));
            if (doc.RootElement.GetArrayLength() < PageSize) break;
        }
        return moves;
    }

    private static TrackerActor? Actor(JsonElement user) =>
        user.ValueKind == JsonValueKind.Object && user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
            ? new TrackerActor(id.GetInt64().ToString(), Str(user, "username"),
                user.TryGetProperty("bot", out var bot) && bot.ValueKind == JsonValueKind.True)
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
