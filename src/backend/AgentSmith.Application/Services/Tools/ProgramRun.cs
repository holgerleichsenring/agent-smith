namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-22-46ef: what a server-built process answered — the exit code, and the
/// labeled-section body a tool hands the model.
/// <para>
/// The exit travels WITH the body because a tool that reads only stdout cannot tell "found
/// nothing" from "that program is not in this image": a missing binary returns an empty
/// string that reads as an absence, and a fabricated absence on a grounding surface is worse
/// than a refusal.
/// </para>
/// </summary>
internal sealed record ProgramRun(int ExitCode, string Rendered);
