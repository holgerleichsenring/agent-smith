namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: a PoC's variables, in name order.</summary>
internal sealed record PocVariablesView(IReadOnlyList<PocVariableView> Variables);
