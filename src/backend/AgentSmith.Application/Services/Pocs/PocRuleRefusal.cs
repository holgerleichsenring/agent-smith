namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: why a PoC archive rule refused, and which of <see cref="PocArchiveChecks"/> it belongs
/// to.</summary>
public sealed record PocRuleRefusal(string Check, string Reason);
