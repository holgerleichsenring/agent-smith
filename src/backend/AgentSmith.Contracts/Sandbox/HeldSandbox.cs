namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: one sandbox a design conversation is holding open between turns,
/// as the process that holds it knows it.
/// </summary>
/// <param name="Key">
/// What identifies this hold to the turn that may take it back — the job id of the
/// sandbox, which is unique per container.
/// </param>
/// <param name="ConversationId">The conversation whose label the sandbox carries.</param>
/// <param name="Sandbox">The release path: a force remove, never a disposal.</param>
public sealed record HeldSandbox(string Key, string ConversationId, ISandboxForceRemoval Sandbox);
