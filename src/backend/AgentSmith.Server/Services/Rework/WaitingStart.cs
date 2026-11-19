namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-19-1b72a: what a start of a waiting ticket came to — whether it started, and why not when it did not.</summary>
public sealed record WaitingStart(bool Started, string Text);
