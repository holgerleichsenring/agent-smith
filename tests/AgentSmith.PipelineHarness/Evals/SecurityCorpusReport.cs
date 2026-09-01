namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: one scoring run over the security corpus.
/// <para>
/// TWO rates, each over its own denominator — misses over the flawed files, false alarms
/// over the clean ones. One combined score hides the direction that has cost live runs: a
/// scan that finds nothing looks exactly like a clean repository, and fourteen tuning
/// phases on the delivery account moved the wrong axis for precisely that reason.
/// </para>
/// <para>
/// Both populations are printed. A rate over four files is not a rate, and a report that
/// hides its n invites the first number to be read as ground truth.
/// </para>
/// </summary>
public sealed record SecurityCorpusReport(
    string ModelId,
    string ScanPromptVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SecurityCorpusReport.CorpusEntry> Entries)
{
    /// <summary>
    /// The sentence the report leads with, in its own header. A number printed without it
    /// will be read as a grade, which is how a measurement becomes worse than none.
    /// </summary>
    public const string CannotGradeSentence =
        "A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the "
        + "training data and a planted defect is formulaic in a way a real one is not, so a "
        + "green floor proves only that the scan is wired, reaches the code and emits "
        + "findings at all. It is not a quality score and must never be quoted as one.";

    /// <summary>One corpus's outcome, or the loud failure of a scan that could not be run
    /// — which is itself a finding and is never scored as agreement.</summary>
    public sealed record CorpusEntry(
        string CorpusId,
        IReadOnlyList<FileOutcome> Files,
        IReadOnlyList<string> StepsThatContributedNothing,
        string? Problem);

    /// <summary>
    /// What the truth was for one file and what the scan said about it.
    /// <para>
    /// Detection is "any delivered finding names this file". The LINE is reported beside
    /// it and never gates: a finding that cited the call rather than the sink has detected
    /// the weakness, and scoring the line would turn a detection number into a citation
    /// number.
    /// </para>
    /// <para>
    /// The SEVERITY is recorded and does not gate either. A first run showed why both
    /// halves of that matter: the master delivered a row on a sound file whose own text
    /// said the scanner hit was a false positive. It is still a row an operator reads
    /// about a sound file, so it counts; what it was raised AS is the thing that tells
    /// them whether the noise was actionable, so the report prints it.
    /// </para>
    /// </summary>
    public sealed record FileOutcome(
        string Path,
        string Class,
        bool TruthIsFlawed,
        bool HasFinding,
        bool CitesDeclaredLine,
        string? FindingHeadline,
        string? HighestSeverity = null) : IScoredSubject
    {
        public bool IsMiss => TruthIsFlawed && !HasFinding;

        public bool IsFalseAlarm => !TruthIsFlawed && HasFinding;

        public bool Agrees => TruthIsFlawed == HasFinding;

        bool IScoredSubject.TruthIsWeak => TruthIsFlawed;
    }

    private IEnumerable<FileOutcome> All => Entries.SelectMany(e => e.Files);

    /// <summary>The arithmetic, shared with the api scoreboard — see
    /// <see cref="ScanDetectionRates"/> for why the two scans share it and not their
    /// match key.</summary>
    public ScanDetectionRates Rates => ScanDetectionRates.Over(All);

    /// <summary>Files that genuinely hold a weakness — the denominator of the miss rate,
    /// and nothing else.</summary>
    public int FlawedPopulation => Rates.WeakPopulation;

    /// <summary>Files that are genuinely sound — the denominator of the false-alarm rate.</summary>
    public int CleanPopulation => Rates.SoundPopulation;

    public int Misses => Rates.Misses;

    public int FalseAlarms => Rates.FalseAlarms;

    public double MissRate => Rates.MissRate;

    public double FalseAlarmRate => Rates.FalseAlarmRate;

    /// <summary>The files the scan did not answer for, named — a rate alone does not tell
    /// anyone what to go and look at.</summary>
    public IReadOnlyList<string> MissedFiles =>
        [.. All.Where(f => f.IsMiss).Select(f => f.Path).OrderBy(p => p, StringComparer.Ordinal)];

    /// <summary>Detected weaknesses whose finding also landed on the declared line. A
    /// SUB-METRIC: it says something about citation quality and nothing about detection.</summary>
    public int LineAccurateDetections =>
        All.Count(f => f.TruthIsFlawed && f.HasFinding && f.CitesDeclaredLine);

    public int Detections => Rates.Detections;

    /// <summary>Steps that ran and produced nothing, named beside the score so a run where
    /// half the scan never executed is never read as a detection result.</summary>
    public IReadOnlyList<string> StepsThatContributedNothing =>
        [.. Entries.SelectMany(e => e.StepsThatContributedNothing)
            .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal)];

    /// <summary>A run whose scan never completed anywhere — the report exists, and it
    /// carries no number.</summary>
    public bool Scored => Entries.Any(e => e.Problem is null);
}
