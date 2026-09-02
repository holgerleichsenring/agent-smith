using AgentSmith.Contracts.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: matches delivered findings to declared endpoints by method and path
/// template. See <see cref="ApiEndpointMatch"/> for what "names this endpoint" means.
/// </summary>
public static class ApiCorpusScoring
{
    public static IReadOnlyList<ApiCorpusReport.EndpointOutcome> Score(
        ApiTargetDeclaration declaration, IReadOnlyList<SkillObservation> findings)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(findings);
        return
        [
            .. declaration.Endpoints.Where(e => e.HasKnownVerdict)
                .Select(endpoint => Outcome(endpoint, findings)),
        ];
    }

    /// <summary>Locations no declaration claims. A finding here is neither a detection nor
    /// a false alarm — it is an observation with no denominator, and the report says so
    /// rather than folding it into a rate.</summary>
    public static IReadOnlyList<string> UndeclaredLocations(
        ApiTargetDeclaration declaration, IReadOnlyList<SkillObservation> findings)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(findings);
        return
        [
            .. findings.Select(Location)
                .Where(location => !string.IsNullOrWhiteSpace(location)
                    && !declaration.Endpoints.Any(e => ApiEndpointMatch.Matches(e, location)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.Ordinal),
        ];
    }

    private static ApiCorpusReport.EndpointOutcome Outcome(
        ApiEndpointDeclaration endpoint, IReadOnlyList<SkillObservation> findings)
    {
        var onEndpoint = findings.Where(o => ApiEndpointMatch.Matches(endpoint, Location(o))).ToList();
        return new ApiCorpusReport.EndpointOutcome(
            endpoint.Describe(),
            endpoint.Class,
            endpoint.IsWeak,
            onEndpoint.Count > 0,
            onEndpoint.FirstOrDefault()?.Description,
            onEndpoint.Count == 0 ? null : onEndpoint.Max(o => o.Severity).ToString());
    }

    /// <summary>Where a finding says it is. ApiPath first, then the display location the
    /// delivery layer renders — which falls back to ApiPath and then to the schema name,
    /// so an endpoint finding with no file is still located.</summary>
    private static string Location(SkillObservation observation) =>
        !string.IsNullOrWhiteSpace(observation.ApiPath)
            ? observation.ApiPath!
            : observation.DisplayLocation;
}
