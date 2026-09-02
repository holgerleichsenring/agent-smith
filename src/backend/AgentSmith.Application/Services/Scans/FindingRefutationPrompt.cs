namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the question put to a fresh instance about the findings a scan is about to
/// deliver — every one of them, whoever raised it.
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
            Below are the security findings a scan is about to deliver. Some were raised by
            automated scanners and never confirmed by anyone; others were written by the
            reviewing agent itself. Treat them all the same way — nine findings of this kind
            were once delivered as CRITICAL and all nine were wrong.

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

            Answer with one row per finding, in JSON and nothing else, echoing the finding's
            id EXACTLY as given — two findings can share a location, and a row whose id names
            no finding answers none of them. When you refute, quote VERBATIM a line of the
            evidence shown under that finding — a refutation whose quote is not in what you
            were shown is discarded and the finding stands:

              [{"id": "<the finding id, verbatim>",
                "location": "<the location string, verbatim>", "substantiated": true|false,
                "quote": "<verbatim line of the shown evidence, or null when substantiated>",
                "why": "<one short sentence>"}]

            FINDINGS
            {{listed}}
            """;
    }

    private static string Describe(CandidateFinding candidate) =>
        $"id: {candidate.Id}\nlocation: {candidate.Location}\n"
        + $"severity: {candidate.Observation.Severity}\n"
        + $"raised by: {candidate.Observation.Role}\nclaim: {candidate.Observation.Description}\n"
        + $"{Label(candidate.Surface)}:\n{candidate.Evidence}";

    private static string Label(EvidenceSurface surface) => surface switch
    {
        EvidenceSurface.LiveTarget => "evidence (what the specification declares and what the scanner really sent and received)",
        _ => "code",
    };
}
