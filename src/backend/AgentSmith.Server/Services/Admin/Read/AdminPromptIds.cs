using System.Text.Json;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>2026-11-17-f3a8b: the prompt ids of a run's calls, read off their LlmCallFinished events by sequence.</summary>
internal static class AdminPromptIds
{
    private const string FinishedType = "LlmCallFinished";

    internal static async Task<IReadOnlyDictionary<long, string>> OfAsync(
        AgentSmithDbContext db, string runId, IEnumerable<long?> sequences, CancellationToken ct)
    {
        var wanted = sequences.OfType<long>().ToList();
        if (wanted.Count == 0) return new Dictionary<long, string>();
        var events = await db.RunEvents.AsNoTracking()
            .Where(e => e.RunId == runId && e.Type == FinishedType && wanted.Contains(e.Seq))
            .Select(e => new { e.Seq, e.PayloadJson }).ToListAsync(ct);
        return events.Select(e => (e.Seq, Id: PromptId(e.PayloadJson))).Where(e => e.Id is not null)
            .ToDictionary(e => e.Seq, e => e.Id!);
    }

    private static string? PromptId(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("PromptId", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() : null;
    }
}
