using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-13-8762u: a ticket's spec series, from agentsmith.yml's top-level <c>series:</c> block — how many
/// specs one series may hold, how many one attempt runs, and the measured seconds of one whole spec block
/// (master, premise, verify, review, fix pass and record) the attempt is sized against the run wall clock by.
/// Global by design: one value for every project; a larger repository's block shrinks every attempt, which is
/// safe. This repository measured 1294 s, a lower bound (decisions/2026-10-13-8762c.yaml).
/// </summary>
public sealed class SeriesConfig
{
    /// <summary>The member letters a..z.</summary>
    public const int MemberLetters = 26;

    /// <summary>The most specs one series may hold.</summary>
    public int MaxSpecs { get; set; } = 24;

    /// <summary>The most specs one attempt runs.</summary>
    public int SpecsPerAttempt { get; set; } = 8;

    /// <summary>The measured seconds of one whole spec block; 0 = not measured.</summary>
    public int SecondsPerSpec { get; set; }

    /// <summary>The most specs one attempt can run under the step guard's ceiling.</summary>
    public static int StepCeilingSpecs => (StepBudget.AbsoluteCeiling - StepBudget.BaseAllowance) / StepBudget.PerSpecAllowance;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule — a
    /// reader refuses what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (MaxSpecs is < 1 or > MemberLetters)
            problems.Add($"series.max_specs is {MaxSpecs}; it must be between 1 and {MemberLetters} (the member letters a..z)");
        var perAttempt = Math.Min(Math.Max(MaxSpecs, 1), StepCeilingSpecs);
        if (SpecsPerAttempt < 1 || SpecsPerAttempt > perAttempt)
            problems.Add($"series.specs_per_attempt is {SpecsPerAttempt}; it must be between 1 and {perAttempt} "
                + $"(series.max_specs, and the {StepCeilingSpecs} specs the step guard's ceiling holds)");
        if (SecondsPerSpec < 0) problems.Add($"series.seconds_per_spec is {SecondsPerSpec}; it must be 0 or more");
        return problems;
    }

    /// <summary><see cref="Problems()"/>, and one more when two blocks — one spec plus the block reserved for the
    /// commit-and-PR tail — exceed the run wall clock. A wall clock of 0 or less is no ceiling.</summary>
    public IReadOnlyList<string> Problems(int runWallSeconds)
    {
        var problems = Problems().ToList();
        if (SecondsPerSpec > 0 && runWallSeconds > 0 && 2L * SecondsPerSpec > runWallSeconds)
            problems.Add($"series.seconds_per_spec is {SecondsPerSpec}; two blocks (one spec and the commit-and-PR tail) "
                + $"exceed orchestrator.max_run_wall_time_seconds ({runWallSeconds})");
        return problems;
    }

    /// <summary>How many specs one attempt runs with <paramref name="secondsLeft"/> of the wall clock left:
    /// <see cref="SpecsPerAttempt"/> when unmeasured, else the smaller of it and the blocks that fit, one block
    /// reserved for the commit-and-PR tail. Zero or less means not even one spec fits — never floored.</summary>
    public int AttemptSize(int secondsLeft) =>
        SecondsPerSpec <= 0 ? SpecsPerAttempt : Math.Min(SpecsPerAttempt, (secondsLeft / SecondsPerSpec) - 1);

    /// <summary>The most specs one derivation reply may carry: a fresh cut runs in one attempt, a re-cut repeats
    /// the executed head and may re-cut a tail longer than one attempt, so it is bounded by the series.</summary>
    public int DerivationLimit(bool recut) => recut ? MaxSpecs : SpecsPerAttempt;

    /// <summary>Why a reply of <paramref name="specs"/> specs is refused, or null.</summary>
    public string? DerivationRefusal(bool recut, int specs) =>
        specs <= DerivationLimit(recut) ? null
        : recut ? $"{specs} specs exceed series.max_specs ({MaxSpecs}), the most one series holds with the executed specs — merge the smaller ones"
        : $"{specs} specs exceed series.specs_per_attempt ({SpecsPerAttempt}), the most one attempt runs — merge the smaller ones";

    /// <summary>The new specs an append after <paramref name="executed"/> executed ones may add.</summary>
    public int AppendRoom(int executed) => Math.Max(0, Math.Min(MaxSpecs - executed, SpecsPerAttempt));
}
