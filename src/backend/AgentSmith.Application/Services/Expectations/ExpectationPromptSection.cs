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
    /// 2026-11-18-98fa: what is work rather than a reason to leave a line — moved here from the retired challenge
    /// nudge. Nothing is judged per line any more; the master's report says what it saw for each.
    /// </summary>
    internal const string WorkNotReasons =
        "A command the repository's ecosystem ships as an installable tool (a dotnet tool, an npm dev "
        + "dependency, a pip package in a venv) that is missing is work: install it with run_command and "
        + "continue. A red test is work. A test the criteria ask for and nobody wrote is work.";

    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var criteria = Specs.AcceptanceCriteria.For(pipeline);
        return criteria.Count == 0 ? string.Empty : BuildFrom(criteria);
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
        The criteria below are this spec's done list. Implement exactly what they assert, no more and no
        less. Your report says what you saw for each; a line you leave says why, and what would make it hold
        goes into a follow-up.
        {WorkNotReasons}

        ## Expected
        {string.Join("\n", criteria.Select(c => $"- {c}"))}

        """;
}
