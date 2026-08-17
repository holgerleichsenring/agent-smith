using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: resolves a finding's citation by READING the file it names.
/// <para>
/// The mechanical half, exactly as a phase account's: we cannot check that a line MEANS
/// what the finding claims, but we can check that the file it names is a file the scan can
/// read. A citation that resolves against nothing is a fabrication — this is the half that
/// would have caught the nine false criticals before a model was ever asked.
/// </para>
/// <para>
/// Only a claim about TODAY'S source can be refuted by reading today's source. A vulnerable
/// package has no line, and a secret in git history is not refuted by its absence from the
/// working tree — the merge has reasoned this way since p0333.
/// </para>
/// </summary>
public sealed class SourceCitationResolver(CitedCodeWindow window) : ICandidateResolver
{
    private const string GitHistoryRole = "git-history-scanner";

    public bool CanAnswer(SkillObservation finding, ScanEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(evidence);
        return evidence.Source is not null
            && finding is { EvidenceMode: EvidenceMode.AnalyzedFromSource, StartLine: > 0 }
            && !string.IsNullOrWhiteSpace(finding.File)
            && !string.Equals(finding.Role, GitHistoryRole, StringComparison.Ordinal);
    }

    public async Task<CandidateFinding?> ResolveAsync(
        SkillObservation finding, ScanEvidence evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(evidence);
        var content = await evidence.Source!.TryReadAsync(finding.File!, cancellationToken);
        return content is null
            ? null
            : new CandidateFinding(
                finding, finding.DisplayLocation, window.Around(content, finding.StartLine));
    }
}
