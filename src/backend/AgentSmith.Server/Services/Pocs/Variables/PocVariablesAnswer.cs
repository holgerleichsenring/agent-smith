namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: the poc-variables run that wrote a change, and its line (names and kinds only).
/// 2026-11-23-c81ed: the poc-apply run it queued so the change reaches the pod at once, or why none was queued.</summary>
internal sealed record PocVariablesAnswer(string RunId, string Line, string? ApplyRunId = null, string? ApplyNote = null);
