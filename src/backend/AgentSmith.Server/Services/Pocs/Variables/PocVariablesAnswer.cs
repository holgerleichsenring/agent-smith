namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: the poc-variables run that wrote a change, and its line (names and kinds only).</summary>
internal sealed record PocVariablesAnswer(string RunId, string Line);
