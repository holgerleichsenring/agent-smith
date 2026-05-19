using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Orchestrates LLM-JSON → <see cref="SkillObservation"/> conversion. JSON
/// recovery (fences, prose, truncated arrays) flows through
/// <see cref="ITolerantJsonParser"/>; per-element rule application (confidence
/// migration, field truncation, category-drift) flows through
/// <see cref="IObservationNormalizer"/>; total-failure fallbacks live in
/// <see cref="ObservationRecoveryHelper"/>. This class only sequences them.
/// </summary>
public sealed class ObservationParser(
    ITolerantJsonParser tolerantParser,
    IObservationNormalizer normalizer)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public List<SkillObservation> ParseWithoutIds(string response, string role, ILogger? logger = null)
    {
        var parsed = Parse(response, role, 0, logger);
        for (var i = 0; i < parsed.Count; i++) parsed[i] = parsed[i] with { Id = 0 };
        return parsed;
    }

    public List<SkillObservation>? TryParseWithoutIds(string response, string role, ILogger? logger = null)
    {
        var arr = tolerantParser.ParseArray(response);
        if (arr.Document is not null && arr.Document.RootElement.ValueKind == JsonValueKind.Array)
        {
            using (arr.Document)
            {
                var elementWise = BuildFromArray(arr.Document.RootElement, role, 0, logger);
                if (elementWise.Count > 0) return elementWise;
            }
        }
        return ObservationRecoveryHelper.TryResilientFallback(
            tolerantParser, response, role, (e, i, w, l) => TryBuild(e, role, 0, i, w, l), logger);
    }

    public List<SkillObservation> Parse(string response, string role, int startId, ILogger? logger = null)
    {
        var arr = tolerantParser.ParseArray(response);
        if (arr.Document is null || arr.Document.RootElement.ValueKind != JsonValueKind.Array)
            return ResilientOrFallback(response, role, startId, logger);
        using (arr.Document)
        {
            var result = BuildFromArray(arr.Document.RootElement, role, startId, logger);
            return result.Count == 0 ? ResilientOrFallback(response, role, startId, logger) : result;
        }
    }

    private List<SkillObservation> ResilientOrFallback(string response, string role, int startId, ILogger? logger) =>
        ObservationRecoveryHelper.TryResilientFallback(
            tolerantParser, response, role, (e, i, w, l) => TryBuild(e, role, 0, i, w, l), logger)
        ?? ObservationRecoveryHelper.FallbackSingle(response, role, startId, logger);

    private List<SkillObservation> BuildFromArray(JsonElement array, string role, int startId, ILogger? logger)
    {
        var result = new List<SkillObservation>();
        var perRunWarn = new HashSet<string>();
        int id = startId, index = 0, skipped = 0;
        foreach (var element in array.EnumerateArray())
        {
            var obs = TryBuild(element, role, id, index, perRunWarn, logger);
            if (obs is null) { skipped++; index++; continue; }
            result.Add(obs); id++; index++;
        }
        if (skipped > 0 && result.Count > 0)
            logger?.LogWarning(
                "Parsed {Valid}/{Total} observations from {Role} — {Skipped} skipped due to invalid JSON shape",
                result.Count, index, role, skipped);
        return result;
    }

    private SkillObservation? TryBuild(
        JsonElement element, string role, int id, int index,
        HashSet<string> perRunWarn, ILogger? logger)
    {
        try
        {
            var entry = element.Deserialize<RawObservation>(JsonOptions);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Description)) return null;
            return normalizer.Normalize(entry.ToFields(), role, id, perRunWarn, logger);
        }
        catch (JsonException ex)
        {
            var preview = element.GetRawText();
            if (preview.Length > 200) preview = preview[..200];
            logger?.LogWarning(
                "Skipping observation index {Index} from {Role} — invalid JSON shape: {Error}. Preview: {Preview}",
                index, role, ex.Message, preview);
            return null;
        }
    }
}
