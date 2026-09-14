using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: renders the {ExpectationSection} prompt token — the run's BINDING acceptance
/// contract for planner and master.
/// <para>
/// 2026-09-06-4a2c: and its source is now whatever the run is actually judged by. p0393
/// retired NegotiateExpectation because the derived spec IS the negotiated expectation, but
/// this section kept reading the retired key alone — so on every spec-derived run the token
/// rendered EMPTY, the master was never given the heading its skill conditions the
/// `acceptance` array on, and the acceptance gate found no dispositions to weigh and could
/// never be satisfied. The criteria come from the same funnel the gate resolves, so the
/// master is asked about exactly the list it will be measured against.
/// </para>
/// <para>
/// Empty when the run has neither — other presets, ticketless runs. A master body without
/// the placeholder simply never renders it (old skills pins keep working — Render's token
/// replace is a no-op then).
/// </para>
/// </summary>
public static class ExpectationPromptSection
{
    /// <summary>
    /// 2026-09-06-3d81: the third answer, stated. AcceptanceStatus has always had it and the
    /// gate has always accepted it with a reason — a vocabulary nobody is told about is a
    /// vocabulary nobody uses, and a criterion the repository makes impossible was fought pass
    /// after pass or accepted in silence instead. Wording matches the master skill's own.
    /// </summary>
    private const string DeclineRule =
        "A criterion this repository makes impossible — one no edit within this run's scope can make\n"
        + "true — is neither fought pass after pass nor accepted in silence: dispose of it as\n"
        + "`not_applicable`, and let its evidence carry the EVALUATED MEANING of not doing it — what is\n"
        + "lost, and what would have to change elsewhere for it to hold. A bare \"N/A\" with no such\n"
        + "reason does not count. Every declined criterion is reported to the ticket author.";

    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var expectation)
            && expectation is not null)
            return Build(expectation);
        var criteria = Specs.AcceptanceCriteria.For(pipeline);
        return criteria.Count == 0 ? string.Empty : BuildFrom(criteria);
    }

    public static string Build(RatifiedExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        var stamp = expectation.IsUnratified
            ? " (UNRATIFIED — auto-ratified without human review)"
            : string.Empty;
        return $"""
            ## Acceptance contract{stamp}
            The ratified expectation below is the binding acceptance contract for this run.
            Implement exactly what it asserts — no more, no less. Every "Expected" assertion
            must hold after your change; every constraint must be respected.
            {DeclineRule}

            {ExpectationMarkdown.Render(expectation.Draft)}

            """;
    }

    /// <summary>
    /// The contract as a criteria list has no Observed section: a derived spec states what must
    /// become true, never what is presently the case, and inventing a present state would be
    /// writing fiction into a contract. The Expected heading is not decoration — the master's
    /// skill says "one entry per ratified 'Expected' assertion" and "walk EACH 'Expected'
    /// assertion", so the instructions it already carries have to land on something so named.
    /// </summary>
    private static string BuildFrom(IReadOnlyList<string> criteria) =>
        $"""
        ## Acceptance contract
        The criteria below are the binding acceptance contract for this run — the same list the
        framework judges it by. Implement exactly what they assert, no more and no less.
        {DeclineRule}

        ## Expected
        {string.Join("\n", criteria.Select(c => $"- {c}"))}

        """;
}
