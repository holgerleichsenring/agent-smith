namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-19-1b72a: what a start of a waiting ticket came to — whether it started, and why not when it did not.
/// 2026-11-22-d434a: <paramref name="Pending"/> when it waits on a prerequisite whose newest attempt has not ended yet,
/// so the wake asks again instead of being spent.</summary>
public sealed record WaitingStart(bool Started, string Text, bool Pending = false);
