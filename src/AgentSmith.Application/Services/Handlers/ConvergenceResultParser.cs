using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the LLM's convergence analysis response into a ConvergenceResult.
/// </summary>
internal static class ConvergenceResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    internal static ConvergenceResult? Parse(
        string response,
        IReadOnlyList<SkillObservation> allObservations,
        ILogger? logger = null)
    {
        try
        {
            var json = ExtractJson(response);
            if (json is null) return null;

            var raw = JsonSerializer.Deserialize<RawConvergenceResponse>(json, JsonOptions);
            if (raw is null) return null;

            var links = (raw.Links ?? [])
                .Where(l => l.ObservationId > 0 && l.RelatedObservationId > 0)
                .Select(l => new ObservationLink(l.ObservationId, l.RelatedObservationId, l.Relationship))
                .ToList();

            var additionalRoles = raw.AdditionalRoles ?? [];

            // Determine consensus based on blocking observations and contradictions
            var blocking = allObservations.Where(o => o.Blocking).ToList();
            var nonBlocking = allObservations.Where(o => !o.Blocking).ToList();

            var hasContradictions = links.Any(l => l.Relationship == ObservationRelationship.Contradicts
                && blocking.Any(b => b.Id == l.ObservationId || b.Id == l.RelatedObservationId));
            var hasLowConfidenceBlocking = blocking.Any(b => b.Confidence < 70);

            var consensus = raw.Consensus
                            && !hasContradictions
                            && !hasLowConfidenceBlocking;

            return new ConvergenceResult(
                consensus,
                allObservations,
                links,
                additionalRoles,
                blocking,
                nonBlocking);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to parse convergence result JSON");
            return null;
        }
    }

    private static string? ExtractJson(string response)
    {
        var text = response.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return null;
    }

    private sealed class RawConvergenceResponse
    {
        public bool Consensus { get; set; }
        public List<RawLink>? Links { get; set; }
        public List<string>? AdditionalRoles { get; set; }
    }

    private sealed class RawLink
    {
        public int ObservationId { get; set; }
        public int RelatedObservationId { get; set; }
        public ObservationRelationship Relationship { get; set; }
    }
}
