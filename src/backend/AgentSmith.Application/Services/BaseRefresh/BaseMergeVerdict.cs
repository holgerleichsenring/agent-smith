using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: what the declared stages said over a merged tree — how many ran, the first red one, or why
/// the tree could not be checked at all.</summary>
public sealed record BaseMergeVerdict(int Ran, VerifyOutcome? Red, string? Problem)
{
    public static BaseMergeVerdict Unchecked(string problem) => new(0, null, problem);
}
