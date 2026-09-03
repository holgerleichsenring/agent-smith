namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: one context's DECLARED target probe.
/// <para>
/// The same per-sandbox context list <see cref="ContextVerifyStages"/> is read from, and
/// for the same reason: two contexts resolving the same image collapse into ONE sandbox,
/// so reading the representative discovery would make whose declaration is asked depend
/// on discovery order.
/// </para>
/// <para>
/// 2026-09-03-7bac: the probe carries no working directory. It runs at the repository
/// root, where every other command in the sandbox runs.
/// </para>
/// </summary>
public sealed record ContextTargetProbe(string ContextName, string Target, string Command);
