namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: what a run's series round tells the review step — its summary line, a standing series
/// REFUSE for 8762h's one Decide, or a revision the branch did not take, which fails the step as g's does.
/// </summary>
public sealed record SeriesContractRoundResult(string Line, StandingObjection? Objection = null, string? Failure = null);
