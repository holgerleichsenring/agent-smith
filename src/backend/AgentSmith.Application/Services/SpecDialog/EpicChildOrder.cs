using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a72a: the children in the order they may be filed, or the reason no such
/// order exists. <see cref="Error"/> non-null means the epic is refused — the children
/// are returned unchanged so a caller can name them in the refusal.
/// </summary>
public sealed record EpicChildOrder(IReadOnlyList<PhaseDraft> Children, string? Error);
