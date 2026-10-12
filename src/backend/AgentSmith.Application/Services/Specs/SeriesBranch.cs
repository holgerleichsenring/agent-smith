using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: what DeriveSpec read off the branch and the scope its next revision is placed in.
/// </summary>
public sealed record SeriesBranch(SpecSetOnBranch OnBranch, SeriesScope Scope);
