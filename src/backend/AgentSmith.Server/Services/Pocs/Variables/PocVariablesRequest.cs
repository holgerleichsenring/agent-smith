namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: what PUT /api/pocs/{pocId}/variables posts — the variables to set and the names to delete.</summary>
internal sealed record PocVariablesRequest(IReadOnlyList<PocVariableInput>? Set, IReadOnlyList<string>? Delete);
