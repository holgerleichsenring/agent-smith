namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-16-f3fba: one context the image chain leaves on the generic fallback image, as the
/// toolchain preflight reports it — <see cref="Declares"/> when it names verify stages that image cannot run.
/// </summary>
public sealed record FallenBackContext(string Repo, string Context, string? Language, string Line, bool Declares);
