namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0427: one entry of a recorded run — the call's position in the run, what kind of call
/// it was ("prompt", "answer", "tool"), and the content as it was recorded.
/// </summary>
public sealed record RecordedTraceEntry(int Sequence, string Label, string Content);
