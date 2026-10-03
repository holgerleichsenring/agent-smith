namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-03-cf20c: a catalog framework overlay as detection needs it — its slug and the
/// signals any one of which applies it. The catalog declares, the framework reads: a new
/// framework is a catalog change, never a code change.
/// </summary>
public sealed record FrameworkOverlay(string Slug, IReadOnlyList<FrameworkOverlaySignal> Signals);
