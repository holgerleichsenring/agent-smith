namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: what the evidence says of a memory. <c>disputed</c> needs a contradiction AND a negative
/// decayed sum, so strains alone never dispute; <c>holding</c> is a positive sum; everything else, a memory with
/// no record included, is <c>unconfirmed</c>.
/// </summary>
public static class MemoryStandingLabels
{
    public const string Holding = "holding";
    public const string Unconfirmed = "unconfirmed";
    public const string Disputed = "disputed";
}
