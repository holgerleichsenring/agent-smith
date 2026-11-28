using AgentSmith.Contracts.Specs;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-db88a: a ticket IN FLIGHT at the deploy that stopped writing series manifests — the files the old writer
/// (f6bf9abba) left on the ticket branch after a run of <see cref="HarnessSeries.TwoSpecs"/> recorded its first spec and
/// failed on the second, captured from that run's writes, and the pointer row it left, which stores no set.
/// </summary>
internal static class InFlightSeriesFixture
{
    public static readonly string First = HarnessSeries.Base + "a";
    public static readonly string Second = HarnessSeries.Base + "b";
    public static readonly string ManifestPath = $".agentsmith/series/{HarnessSeries.Base}.yaml";
    public static readonly string SecondPlanned = $".agentsmith/specs/planned/{Second}-{Second}.yaml";

    public const string Manifest = """
        ticket: harness-1
        ticket_pinned_whole: false
        specs:
        - 2026-11-19-0b0ba
        - 2026-11-19-0b0bb
        series_repos:
        - primary
        revisions:
        - number: 1
          cause: approved in design conversation harness
          at: 2026-11-28T10:02:19.5345560+00:00
        carried: []
        discarded: []
        unaccounted: []
        shared: []
        discarded_contexts: []
        handback_readings: []
        handback_taken: 0
        ticket_fingerprint: b1ad187b76c2b9373c516e95f229965768f8d1c5ab90810e4885abd7625fe0a5
        approved_at: 2026-11-28T09:57:18.6494590+00:00
        approved_in_conversation: harness
        approved_by: sample.approver

        """;

    private const string SecondSpec = """
        spec: 2026-11-19-0b0bb
        goal: "Move the existing callers onto the guard"
        repos: [primary]
        steps:
          - id: callers
            action: "Move the existing callers onto the guard"
        done:
          - "No caller builds its own empty-payload check."

        """;

    private const string FirstDone = """
        spec: 2026-11-19-0b0ba
        goal: "Introduce the empty-payload guard"
        repos: [primary]
        steps:
          - id: guard
            action: "Introduce the empty-payload guard"
        done:
          - "An empty request body is answered with 400."
        outcome:
          run: harness-c3a87168
          delivered: |-
            ## What this spec delivers

            **default**

            Planning: introduce the guard.

            ## Delivered
            - the change, built and tested
          hand_forward: missing

        """;

    /// <summary>The ticket branch as the next run clones it, repository-relative.</summary>
    public static Dictionary<string, string> Branch() => new(StringComparer.Ordinal)
    {
        [ManifestPath] = Manifest,
        [SecondPlanned] = SecondSpec,
        [$".agentsmith/specs/done/{First}-{First}.yaml"] = FirstDone,
    };

    /// <summary>The row the old recorder left after the first spec's record commit — no stored set.</summary>
    public static SpecSetPointer Pointer() => new(
        "harness-1", "primary", "stub-head", 1, SeriesId: HarnessSeries.Base, ExecutedThrough: 1,
        RevisionShas: new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "stub-head" },
        ExecutedIds: [First]);
}
