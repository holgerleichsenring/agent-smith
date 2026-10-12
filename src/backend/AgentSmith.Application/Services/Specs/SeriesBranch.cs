using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: what DeriveSpec read off the branch, the scope its next revision is placed in,
/// or why it must not go on.
/// </summary>
public sealed record SeriesBranch(SpecSetOnBranch OnBranch, SeriesScope Scope, string? Refusal = null)
{
    public static SeriesBranch Refused(SpecSetOnBranch onBranch, string why) =>
        new(onBranch, new SeriesScope([], null, [], null), why);
}
