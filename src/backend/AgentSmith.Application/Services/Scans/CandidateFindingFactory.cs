using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: resolves each unvouched finding's citation against the scanned source and
/// carries the code it points at.
/// <para>
/// The mechanical half, exactly as a phase account's: we cannot check that a line MEANS
/// what the finding claims, but we can check that the file it names is a file the scan
/// can read. A citation that resolves against nothing is a fabrication and never reaches
/// delivery — this is the half that would have caught the nine false criticals before a
/// model was ever asked.
/// </para>
/// </summary>
public sealed class CandidateFindingFactory(
    CitedCodeWindow window,
    ILogger<CandidateFindingFactory> logger) : ICandidateFindingFactory
{
    private const string GitHistoryRole = "git-history-scanner";

    public async Task<CandidateSet> BuildAsync(
        IReadOnlyList<SkillObservation> unvouched,
        ISandboxFileReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unvouched);
        ArgumentNullException.ThrowIfNull(reader);
        var refutable = new List<CandidateFinding>();
        var unresolvable = new List<SkillObservation>();
        var notFromSource = new List<SkillObservation>();

        foreach (var finding in unvouched)
        {
            if (!IsAboutCurrentSource(finding)) { notFromSource.Add(finding); continue; }
            var content = await reader.TryReadAsync(finding.File!, cancellationToken);
            if (content is null)
            {
                logger.LogWarning(
                    "Dropping an unvouched {Severity} finding — it cites '{File}', which the scan cannot read: {Claim}",
                    finding.Severity, finding.File, finding.Description);
                unresolvable.Add(finding);
                continue;
            }
            refutable.Add(new CandidateFinding(
                finding, finding.DisplayLocation, window.Around(content, finding.StartLine)));
        }

        logger.LogInformation(
            "{Refutable} unvouched finding(s) cite readable source; {Unresolvable} cite "
            + "nothing readable, {NotFromSource} are not answerable from current source",
            refutable.Count, unresolvable.Count, notFromSource.Count);
        return new CandidateSet(refutable, unresolvable, notFromSource);
    }

    /// <summary>
    /// Only a claim about TODAY'S source can be refuted by reading today's source. A
    /// vulnerable package has no line, and a secret in git history is not refuted by its
    /// absence from the working tree — the merge already reasons this way.
    /// </summary>
    private static bool IsAboutCurrentSource(SkillObservation finding) =>
        finding is { EvidenceMode: EvidenceMode.AnalyzedFromSource, StartLine: > 0 }
        && !string.IsNullOrWhiteSpace(finding.File)
        && !string.Equals(finding.Role, GitHistoryRole, StringComparison.Ordinal);
}
