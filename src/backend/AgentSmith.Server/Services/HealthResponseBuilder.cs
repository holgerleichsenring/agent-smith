using System.Text.Json;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services;

/// <summary>
/// Serialises the per-subsystem health snapshot into the JSON body used by /health and
/// /health/ready. /health is liveness (always 200 if listener alive); /health/ready is
/// loud-fail readiness — 503 unless every subsystem reports Up.
/// </summary>
internal static class HealthResponseBuilder
{
    public static (int StatusCode, string Body) Liveness(IEnumerable<ISubsystemHealth> healths)
    {
        var snapshot = Snapshot(healths);
        var allUp = snapshot.All(s => s.State == SubsystemState.Up);
        var body = Serialize(allUp ? "ok" : "degraded", snapshot);
        return (200, body);
    }

    public static (int StatusCode, string Body) Readiness(IEnumerable<ISubsystemHealth> healths)
    {
        var snapshot = Snapshot(healths);
        var allUp = snapshot.All(s => s.State == SubsystemState.Up);
        var body = Serialize(allUp ? "ready" : "not_ready", snapshot);
        return (allUp ? 200 : 503, body);
    }

    private static IReadOnlyList<ISubsystemHealth> Snapshot(IEnumerable<ISubsystemHealth> healths)
        => healths.ToList();

    private static string Serialize(string status, IReadOnlyList<ISubsystemHealth> healths)
    {
        var doc = new
        {
            status,
            subsystems = healths.Select(h => new
            {
                name = h.Name,
                state = h.State.ToString().ToLowerInvariant(),
                reason = h.Reason,
                last_changed_utc = h.LastChangedUtc?.ToString("O")
            }).ToArray()
        };
        return JsonSerializer.Serialize(doc, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };
}
