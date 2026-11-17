using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-7c9bf: the CLI's <c>modelUsage</c> split into the model the call ran and the models the CLI used
/// on the side. The primary is the model the stream's init event names when modelUsage names it exactly, else
/// the entry that wrote the most output tokens — the model that answered. Every other entry is secondary usage.
/// Until now the keys were joined with '+', so every recorded call read "haiku+opus".
/// </summary>
internal static class WorkerModelUsages
{
    public static (string Primary, IReadOnlyList<WorkerModelUsage> Secondary) Split(JsonElement models, string? sessionModel)
    {
        if (models.ValueKind != JsonValueKind.Object) return (string.Empty, []);
        var usages = models.EnumerateObject().Select(Usage).ToList();
        if (usages.Count == 0) return (string.Empty, []);
        var primary = usages.FirstOrDefault(u => string.Equals(u.Model, sessionModel, StringComparison.Ordinal))
            ?? usages.OrderByDescending(u => u.OutputTokens).ThenBy(u => u.Model, StringComparer.Ordinal).First();
        return (primary.Model, [.. usages.Where(u => !ReferenceEquals(u, primary)).OrderBy(u => u.Model, StringComparer.Ordinal)]);
    }

    private static WorkerModelUsage Usage(JsonProperty entry) => new(
        entry.Name,
        WorkerCliEnvelope.Number(entry.Value, "inputTokens"),
        WorkerCliEnvelope.Number(entry.Value, "outputTokens"),
        WorkerCliEnvelope.Number(entry.Value, "cacheReadInputTokens"),
        WorkerCliEnvelope.Number(entry.Value, "cacheCreationInputTokens"),
        entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("costUSD", out var cost)
            && cost.ValueKind == JsonValueKind.Number && cost.TryGetDecimal(out var usd) ? usd : 0m);
}
