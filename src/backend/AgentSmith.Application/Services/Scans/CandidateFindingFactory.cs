using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: sorts every finding into its fate — refutable, invented, or not answerable at
/// all — before a model is asked anything.
/// <para>
/// p0429a: the routing is the whole point. A repo finding is answered by reading the file
/// it names and a live-target finding by the API document it names, and a finding neither
/// resolver can answer passes through UNTOUCHED. Sending an endpoint citation to the file
/// reader would drop every DAST finding as invention; skipping the check would ship every
/// one of them unchecked.
/// </para>
/// </summary>
public sealed class CandidateFindingFactory(
    SourceCitationResolver source,
    EndpointCitationResolver endpoints,
    ILogger<CandidateFindingFactory> logger) : ICandidateFindingFactory
{
    public async Task<CandidateSet> BuildAsync(
        IReadOnlyList<SkillObservation> findings,
        ScanEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(evidence);
        var refutable = new List<CandidateFinding>();
        var unresolvable = new List<SkillObservation>();
        var unanswerable = new List<SkillObservation>();

        foreach (var finding in findings)
        {
            var resolver = Resolver(finding, evidence);
            if (resolver is null) { unanswerable.Add(finding); continue; }
            var resolution = await resolver.ResolveAsync(finding, evidence, cancellationToken);
            if (resolution.Candidate is not null)
            {
                refutable.Add(resolution.Candidate with { Id = $"f{refutable.Count + 1}" });
                continue;
            }
            if (resolution.CitationExists) { unanswerable.Add(finding); continue; }
            logger.LogWarning(
                "A {Severity} finding cites '{Location}', which the scan's evidence does not "
                + "contain: {Claim}",
                finding.Severity, finding.DisplayLocation, finding.Description);
            unresolvable.Add(finding);
        }

        logger.LogInformation(
            "{Refutable} delivered finding(s) resolved against real evidence; {Unresolvable} "
            + "cite nothing the scan holds, {Unanswerable} are not answerable from it",
            refutable.Count, unresolvable.Count, unanswerable.Count);
        return new CandidateSet(refutable, unresolvable, unanswerable);
    }

    private ICandidateResolver? Resolver(SkillObservation finding, ScanEvidence evidence) =>
        source.CanAnswer(finding, evidence) ? source
        : endpoints.CanAnswer(finding, evidence) ? endpoints
        : null;
}
