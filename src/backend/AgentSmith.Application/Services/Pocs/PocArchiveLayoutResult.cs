namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the entries <see cref="PocArchiveLayout"/> hands the rules, or why the archive leaves
/// itself.</summary>
internal sealed record PocArchiveLayoutResult(IReadOnlyList<PocArchiveItem> Items, PocRuleRefusal? Refusal);
