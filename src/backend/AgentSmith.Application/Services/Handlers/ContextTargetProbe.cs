namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: one context's DECLARED target probe, paired with the working directory
/// that context occupies inside the sandbox.
/// <para>
/// The same pairing <see cref="ContextVerifyStages"/> carries, and for the same reason: two
/// contexts resolving the same image collapse into ONE sandbox, so the probe path reads the
/// full per-sandbox context list and keeps each declaration's own workdir. A probe run at
/// the representative's workdir would ask its question from somebody else's sub-tree.
/// </para>
/// </summary>
public sealed record ContextTargetProbe(
    string ContextName, string Target, string Command, string Workdir);
