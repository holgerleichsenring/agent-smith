namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: one scoring run of the api scan against the served target.
/// <para>
/// The arithmetic is <see cref="ScanDetectionRates"/>, shared with the repository
/// scoreboard; the MATCH KEY is not, and that is the whole reason the two stay separate.
/// </para>
/// <para>
/// A score that does not say what did not run is a wrong score. The dynamic steps of this
/// scan have been observed reporting completion at their time limit with zero findings, and
/// this tier stubs them unless an operator opts in — so what stayed silent is carried
/// beside the number, never inferred from it.
/// </para>
/// </summary>
public sealed record ApiCorpusReport(
    string ModelId,
    string ScanPromptVersion,
    DateTimeOffset GeneratedAt,
    string TargetId,
    IReadOnlyList<ApiCorpusReport.EndpointOutcome> Endpoints,
    IReadOnlyList<string> StepsThatContributedNothing,
    string? Problem)
{
    /// <summary>The sentence the report leads with, in its own header — the same caution
    /// the repository scoreboard carries, for the same reason.</summary>
    public const string CannotGradeSentence =
        "A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are "
        + "authored, few and structural, so a green floor proves only that the api scan "
        + "reaches the target, reads what it serves and emits findings at all. It is not a "
        + "quality score and must never be quoted as one.";

    /// <summary>What the truth was for one endpoint and what the scan said about it.</summary>
    public sealed record EndpointOutcome(
        string Endpoint,
        string Class,
        bool TruthIsWeak,
        bool HasFinding,
        string? FindingHeadline,
        string? HighestSeverity) : IScoredSubject
    {
        public bool IsMiss => TruthIsWeak && !HasFinding;

        public bool IsFalseAlarm => !TruthIsWeak && HasFinding;

        public bool Agrees => TruthIsWeak == HasFinding;
    }

    public ScanDetectionRates Rates => ScanDetectionRates.Over(Endpoints);

    public int WeakPopulation => Rates.WeakPopulation;

    public int SoundPopulation => Rates.SoundPopulation;

    public int Misses => Rates.Misses;

    public int FalseAlarms => Rates.FalseAlarms;

    public double MissRate => Rates.MissRate;

    public double FalseAlarmRate => Rates.FalseAlarmRate;

    public int Detections => Rates.Detections;

    /// <summary>The endpoints the scan did not answer for, named — a rate alone does not
    /// tell anyone what to go and look at.</summary>
    public IReadOnlyList<string> MissedEndpoints =>
        [.. Endpoints.Where(e => e.IsMiss).Select(e => e.Endpoint)
            .OrderBy(e => e, StringComparer.Ordinal)];

    /// <summary>Findings that named no declared endpoint at all. Reported, never scored:
    /// they have no denominator, and pretending otherwise is what let nine unsubstantiated
    /// criticals ship.</summary>
    public IReadOnlyList<string> UndeclaredLocations { get; init; } = [];

    public bool Scored => Problem is null;
}
