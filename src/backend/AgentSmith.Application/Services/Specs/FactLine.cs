namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one fact line as the derivation reply carries it — the claim and
/// the evidence id it cites, before the resolver has decided what it is.
/// </summary>
public sealed record FactLine(string Claim, string Cites);
