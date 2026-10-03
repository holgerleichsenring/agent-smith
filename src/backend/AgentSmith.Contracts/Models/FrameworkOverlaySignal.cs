namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-03-cf20c: one declared-dependency signal of a framework overlay — a manifest
/// <paramref name="File"/> (a name, a glob over direct children, or a path relative to the
/// component root) that must hold <paramref name="Contains"/>. Without
/// <paramref name="Contains"/>, the file's existence is the signal.
/// </summary>
public sealed record FrameworkOverlaySignal(string File, string? Contains);
