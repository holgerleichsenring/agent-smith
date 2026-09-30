using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pricing;

/// <summary>
/// Reads one embedded price-list file (the snapshot or the supplement) into a
/// <see cref="ModelPriceSnapshot"/>. The refresh script already normalised it, so this
/// only maps: per-million decimals, optional cache-read rate and context window.
/// </summary>
public sealed class ModelPriceSnapshotReader
{
    public const string SnapshotResource = "AgentSmith.Application.Resources.model-prices.json";
    public const string SupplementResource = "AgentSmith.Application.Resources.model-prices.supplement.json";

    public ModelPriceSnapshot Read(string resourceName)
    {
        using var document = JsonDocument.Parse(ReadResource(resourceName));
        var root = document.RootElement;
        return new ModelPriceSnapshot(
            OptionalString(root, "source") ?? "",
            root.TryGetProperty("fetchedAt", out var at) ? at.GetDateTimeOffset() : DateTimeOffset.MinValue,
            ReadModels(root.GetProperty("models")),
            ReadAliases(root));
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = typeof(ModelPriceSnapshotReader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded price list '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, BundledModelPrice> ReadModels(JsonElement models)
    {
        var result = new Dictionary<string, BundledModelPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models.EnumerateObject())
            result.TryAdd(model.Name, Map(model.Name, model.Value));
        return result;
    }

    private static Dictionary<string, string> ReadAliases(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("aliases", out var aliases)) return result;
        foreach (var alias in aliases.EnumerateObject())
            result.TryAdd(alias.Name, alias.Value.GetString()!);
        return result;
    }

    private static BundledModelPrice Map(string id, JsonElement price) => new(
        id,
        OptionalString(price, "provider") ?? "",
        price.GetProperty("inputPerMillion").GetDecimal(),
        price.GetProperty("outputPerMillion").GetDecimal(),
        price.TryGetProperty("cacheReadPerMillion", out var cache) ? cache.GetDecimal() : null,
        price.TryGetProperty("contextWindowTokens", out var window) ? window.GetInt32() : null);

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
