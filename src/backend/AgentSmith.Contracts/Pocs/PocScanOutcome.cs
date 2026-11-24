namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dc: a code scan's result for the revision — its new state (blocked, overruled, or validated
/// when it passed unaided), the verdict and, for a blocked one, the agent's explanation.</summary>
public sealed record PocScanOutcome(long PocId, int Revision, string State, string VerdictJson, string? Explanation);
