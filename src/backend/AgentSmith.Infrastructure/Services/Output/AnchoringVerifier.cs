using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// p0151h: enforces the umbrella's anchor-quality bar at output-render time.
/// Each operator-facing observation must carry at least one verifiable
/// anchor:
/// <list type="bullet">
///   <item><c>file</c> + <c>start_line</c> (source-anchored claims).</item>
///   <item><c>api_path</c> (swagger / endpoint-anchored claims).</item>
///   <item><c>schema_name</c> (swagger schema-anchored claims).</item>
///   <item>A scanner template id substring in <c>description</c> (Nuclei /
///         Spectral / ZAP correlations).</item>
/// </list>
/// Returns a list of <see cref="AnchoringAssertion"/> the output strategy
/// renders alongside the findings list. Failures do not block delivery;
/// they make a regression visible to the operator.
/// </summary>
public sealed class AnchoringVerifier
{
    private static readonly string[] ScannerHintMarkers = new[]
    {
        "template_id", "matched_at", "nuclei-templates", "spectral:", "zap-rule"
    };

    public IReadOnlyList<AnchoringAssertion> Verify(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0)
            return new[] { AnchoringAssertion.Pass("anchoring", "no findings — nothing to verify") };

        var orphans = observations
            .Where(o => !IsExecutionLimit(o.Category))
            .Where(o => !HasAnchor(o))
            .ToList();

        var sourceClaims = observations
            .Where(o => o.EvidenceMode == EvidenceMode.AnalyzedFromSource)
            .ToList();
        var sourceClaimsWithFile = sourceClaims.Count(o => !string.IsNullOrWhiteSpace(o.File));

        var results = new List<AnchoringAssertion>
        {
            orphans.Count == 0
                ? AnchoringAssertion.Pass("anchoring", $"all {observations.Count} observations carry a verifiable anchor")
                : AnchoringAssertion.Fail("anchoring",
                    $"{orphans.Count}/{observations.Count} observations have no anchor (file / api_path / schema_name / scanner template_id)"),
        };

        if (sourceClaims.Count > 0)
        {
            results.Add(sourceClaimsWithFile == sourceClaims.Count
                ? AnchoringAssertion.Pass("source-claims",
                    $"all {sourceClaims.Count} analyzed_from_source observations cite a file")
                : AnchoringAssertion.Fail("source-claims",
                    $"{sourceClaims.Count - sourceClaimsWithFile}/{sourceClaims.Count} analyzed_from_source observations are missing a file"));
        }

        return results;
    }

    private static bool HasAnchor(SkillObservation o) =>
        !string.IsNullOrWhiteSpace(o.File)
        || !string.IsNullOrWhiteSpace(o.ApiPath)
        || !string.IsNullOrWhiteSpace(o.SchemaName)
        || DescriptionMentionsScannerAnchor(o.Description);

    private static bool DescriptionMentionsScannerAnchor(string description) =>
        !string.IsNullOrEmpty(description)
        && ScannerHintMarkers.Any(marker =>
            description.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool IsExecutionLimit(string? category) =>
        ExecutionLimitCategories.IsExecutionLimit(category);
}

public sealed record AnchoringAssertion(string Name, bool Passed, string Detail)
{
    public static AnchoringAssertion Pass(string name, string detail) => new(name, true, detail);
    public static AnchoringAssertion Fail(string name, string detail) => new(name, false, detail);
}
