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
            them: the reviewing agent looked at this code and did not report them, so each
            one is either a real defect it missed or a false positive it silently rejected.
            Nine findings of exactly this kind were once delivered as CRITICAL and all nine
            were wrong.

            Your job is to REFUTE each finding against the code shown under it. A finding is
            NOT substantiated when the code shows it cannot be what it claims — a test
            fixture or sample value rather than a live secret, a placeholder, a commented-out
            line, an already-guarded call, a pattern match on something that is not the thing
            named.

            You have only the code shown. If the code does not let you refute the finding,
            it is substantiated — say so. "I cannot tell" is substantiated, not refuted:
            leaving a real defect in is worse than leaving a false positive in.

            Answer with JSON and nothing else. When you refute, quote VERBATIM the line of
            the shown code that refutes it — a refutation whose quote is not in the code you
            were shown is discarded and the finding stands:

              [{"location": "<the location string, verbatim>", "substantiated": true|false,
                "quote": "<verbatim line of the shown code, or null when substantiated>",
                "why": "<one short sentence>"}]

            FINDINGS
            {{listed}}
            """;
    }

    private static string Describe(CandidateFinding candidate) =>
        $"location: {candidate.Location}\nseverity: {candidate.Observation.Severity}\n"
        + $"raised by: {candidate.Observation.Role}\nclaim: {candidate.Observation.Description}\n"
        + $"code:\n{candidate.Code}";
}
