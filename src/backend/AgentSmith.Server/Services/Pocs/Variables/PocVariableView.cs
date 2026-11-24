namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: one variable as GET shows it — name, kind, who set it and when, and the value of a PLAIN
/// variable only (it is committed in plain text); a secret's value is never read back.</summary>
internal sealed record PocVariableView(string Name, string Kind, string SetBy, DateTimeOffset SetAt, string? Value);
