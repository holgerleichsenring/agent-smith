using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the LLM's convergence analysis response into a ConvergenceResult.
/// Fence stripping and prose extraction flow through
/// <see cref="ITolerantJsonParser"/>; mapping to the typed DTO uses the
/// element-level Deserialize once the document is in hand.
/// </summary>
public sealed class ConvergenceResultParser(ITolerantJsonParser tolerantParser)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ConvergenceResult? Parse(
        string response,
        IReadOnlyList<SkillObservation> allObservations,
        ILogger? logger = null)
    {
        var parsed = tolerantParser.ParseObject(response);
        if (parsed.Document is null)
        {
            logger?.LogWarning("Convergence response had no parseable JSON object");
            return null;
        }
        using (parsed.Document)
        {
            try
            {
                var raw = parsed.Document.RootElement.Deserialize<RawConvergenceResponse>(JsonOptions);
                if (raw is null) return null;
                return Build(raw, allObservations);
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex, "Failed to map convergence result JSON to DTO");
                return null;
            }
        }
    }

    private static ConvergenceResult Build(
        RawConvergenceResponse raw, IReadOnlyList<SkillObservation> allObservations)
    {
        var links = (raw.Links ?? [])
            .Where(l => l.ObservationId > 0 && l.RelatedObservationId > 0)
            .Select(l => new ObservationLink(l.ObservationId, l.RelatedObservationId, l.Relationship))
            .ToList();
        var additionalRoles = raw.AdditionalRoles ?? [];
        var blocking = allObservations.Where(o => o.Blocking).ToList();
        var nonBlocking = allObservations.Where(o => !o.Blocking).ToList();
        var hasContradictions = links.Any(l => l.Relationship == ObservationRelationship.Contradicts
            && blocking.Any(b => b.Id == l.ObservationId || b.Id == l.RelatedObservationId));
        var hasLowConfidenceBlocking = blocking.Any(b => b.Confidence < 70);
        var consensus = raw.Consensus && !hasContradictions && !hasLowConfidenceBlocking;
        return new ConvergenceResult(
            consensus, allObservations, links, additionalRoles, blocking, nonBlocking);
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
