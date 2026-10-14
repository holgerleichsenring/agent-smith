namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59l: the series a worked ticket's approval continues — its repositories and its
/// carrier. The run narrows to the record's repositories, so a continuation's must keep the
/// repositories the executed specs ran in; the carrier stays where the series' reference sets go.
/// </summary>
public sealed record ContinuationBasis(IReadOnlyList<string> Repositories, string? Carrier);
