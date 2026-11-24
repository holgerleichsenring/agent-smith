using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: a pattern hit blocks only when it is from the 'secrets' catalog, of severity high or critical, and
/// its pattern's confidence is at least pocs.scan.secret_min_confidence — and then it can never be overruled. A high or
/// critical secret below that confidence is a note naming its confidence, the value and the key; every other hit of
/// every catalog is a note.
/// </summary>
public sealed class PocSecretRule(PocFindingIds ids)
{
    private const string SecretsCategory = "secrets";

    public PocScanFinding Judge(PocPatternHit hit, int occurrence, PocScanConfig config)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(config);
        var id = ids.Secret(hit, occurrence);
        if (!IsSecret(hit) || !IsSevere(hit.Severity))
            return Finding(hit, id, PocScanFindingKinds.Note, $"{hit.Description} ({hit.Category}, {hit.Severity}): a note, not blocking.");
        if (hit.Confidence < config.SecretMinConfidence)
            return Finding(hit, id, PocScanFindingKinds.Note, $"{hit.Description}. Confidence {hit.Confidence} is below "
                + $"{PocScanConfig.SecretMinConfidenceKey} ({config.SecretMinConfidence}): reported, not blocking, because "
                + "patterns under it also match ordinary assignments.");
        return Finding(hit, id, PocScanFindingKinds.Secret, $"{hit.Description} ({hit.Severity}, confidence {hit.Confidence}).")
            with { Fix = Fix(hit) };
    }

    private static bool IsSecret(PocPatternHit hit) => string.Equals(hit.Category, SecretsCategory, StringComparison.OrdinalIgnoreCase);

    private static bool IsSevere(string severity) =>
        severity.Equals("critical", StringComparison.OrdinalIgnoreCase) || severity.Equals("high", StringComparison.OrdinalIgnoreCase);

    private static string Fix(PocPatternHit hit) =>
        "Remove the value from the code, revoke it" + (hit.RevokeUrl is { } url ? $" ({url})" : string.Empty)
        + " because it was uploaded, and give the PoC the new value as a runtime variable; then upload the ZIP again.";

    private static PocScanFinding Finding(PocPatternHit hit, string id, string kind, string detail) =>
        new(id, kind, hit.PatternId, hit.File, hit.Line, hit.Title, detail, hit.MaskedSnippet, null, []);
}
