namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-10-25-9324a: what each kind of evidence is worth to a memory's standing, and how fast it fades — the one
/// place these numbers live (decisions/2026-10-25-9324d.yaml says why each is what it is). A reliance is worth
/// nothing by itself: only what became of the spec that relied counts.
/// </summary>
public static class MemoryStandingWeights
{
    public const double Relied = 0;
    public const double Confirmed = 1;
    public const double Strained = -0.5;
    public const double JudgedNotMet = -1;
    public const double Disputed = -2;
    public const double HalfLifeDays = 90;
}
