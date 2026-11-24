namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-c81ec: an Argo CD probe's outcome — whether the token was taken — and the sentence that says it,
/// with the token's expiry; never a token or a body.</summary>
public sealed record ArgoProbe(bool Reached, string Sentence);
