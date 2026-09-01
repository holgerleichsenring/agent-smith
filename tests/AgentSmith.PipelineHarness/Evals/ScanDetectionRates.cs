namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: one scored subject — a file for a repository scan, an endpoint for an
/// api scan. The two scans cannot share a MATCH KEY (a repo scan reads source, and an api
/// finding frequently carries no file at all), but they share the arithmetic, and two
/// copies of it are two numbers that can start disagreeing.
/// </summary>
public interface IScoredSubject
{
    /// <summary>Whether the subject genuinely holds a weakness.</summary>
    bool TruthIsWeak { get; }

    /// <summary>Whether any delivered finding named it.</summary>
    bool HasFinding { get; }
}

/// <summary>
/// 2026-09-01-6686: two rates over two denominators — misses over the weak subjects, false
/// alarms over the sound ones.
/// <para>
/// One combined score hides the direction that has cost live runs: a scan that finds
/// nothing looks exactly like a clean target, and reporting undeclared findings with no
/// denominator at all cannot see the failure that actually happened — a scan that delivered
/// nine critical findings nobody could substantiate.
/// </para>
/// </summary>
public sealed record ScanDetectionRates(
    int WeakPopulation, int SoundPopulation, int Misses, int FalseAlarms)
{
    public static ScanDetectionRates Over(IEnumerable<IScoredSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        var rows = subjects.ToList();
        return new ScanDetectionRates(
            rows.Count(s => s.TruthIsWeak),
            rows.Count(s => !s.TruthIsWeak),
            rows.Count(s => s.TruthIsWeak && !s.HasFinding),
            rows.Count(s => !s.TruthIsWeak && s.HasFinding));
    }

    public double MissRate => WeakPopulation == 0 ? 0 : (double)Misses / WeakPopulation;

    public double FalseAlarmRate =>
        SoundPopulation == 0 ? 0 : (double)FalseAlarms / SoundPopulation;

    /// <summary>Weak subjects a finding did name.</summary>
    public int Detections => WeakPopulation - Misses;
}
