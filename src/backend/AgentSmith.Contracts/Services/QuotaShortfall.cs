namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-ab45: a call that needs more than its deployment's whole minute — <paramref name="Needed"/> tokens, of
/// them <paramref name="Input"/> input and <paramref name="Output"/> the output the provider counts, against a
/// per-minute budget of <paramref name="Budget"/>.
/// </summary>
public sealed record QuotaShortfall(long Needed, long Input, long Output, int Budget)
{
    /// <summary>2026-11-24-9dc0d: the output is the tokens generated (Azure), not the request's max_tokens (OpenAI).</summary>
    public bool IsGenerated { get; init; }
}
