namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the question put to a fresh instance about findings NOBODY has vouched for —
/// the ones a scanner raised and the scan master never addressed.
/// <para>
/// Asked adversarially, like the cut review and the delivery account: not "is this
/// finding real", to which "yes" is the cheap answer, but "REFUTE it". And a rebuttal
/// must quote a line of the code it was shown, so the framework can check that the
/// refuter read the code rather than reasoning about the headline. A refuter that
/// invents its objection would silence a real critical.
/// </para>
/// </summary>
public static class FindingRefutationPrompt
{
    public static string For(IReadOnlyList<CandidateFinding> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var listed = string.Join("\n\n", candidates.Select(Describe));
        return $$"""
            Below are security findings raised by automated scanners. NOBODY has confirmed
            them: the reviewing agent saw this evidence and did not report them, so each one
            is either a real defect it missed or a false positive it silently rejected. Nine
            findings of exactly this kind were once delivered as CRITICAL and all nine were
            wrong.

            Your job is to REFUTE each finding against the evidence shown under it. A source
            finding is NOT substantiated when the code shows it cannot be what it claims — a
            test fixture or sample value rather than a live secret, a placeholder, a
            commented-out line, an already-guarded call, a pattern match on something that is
            not the thing named. A live-target finding is NOT substantiated when the recorded
            request and response show the endpoint did not behave as claimed — an
            authenticated response read as anonymous, a 401 read as a leak, a header the
            response does not carry.

            You have only the evidence shown. If it does not let you refute the finding, it
            is substantiated — say so. "I cannot tell" is substantiated, not refuted: leaving
            a real defect in is worse than leaving a false positive in.

            Answer with JSON and nothing else. When you refute, quote VERBATIM a line of the
            evidence shown under that finding — a refutation whose quote is not in what you
            were shown is discarded and the finding stands:

              [{"location": "<the location string, verbatim>", "substantiated": true|false,
                "quote": "<verbatim line of the shown evidence, or null when substantiated>",
                "why": "<one short sentence>"}]

            FINDINGS
            {{listed}}
            """;
    }

    private static string Describe(CandidateFinding candidate) =>
        $"location: {candidate.Location}\nseverity: {candidate.Observation.Severity}\n"
        + $"raised by: {candidate.Observation.Role}\nclaim: {candidate.Observation.Description}\n"
        + $"{Label(candidate.Surface)}:\n{candidate.Evidence}";

    private static string Label(EvidenceSurface surface) => surface switch
    {
        EvidenceSurface.LiveTarget => "evidence (what the specification declares and what the scanner really sent and received)",
        _ => "code",
    };
}
