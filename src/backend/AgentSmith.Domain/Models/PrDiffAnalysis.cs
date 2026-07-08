namespace AgentSmith.Domain.Models;

/// <summary>
/// p0167a: structured diff of a pull request — the value behind
/// ContextKeys.PrDiff. Produced by AnalyzePrDiffHandler from the platform's
/// raw per-file patches; consumed by the pr-review skills (p0167b) and the
/// findings compiler (p0167c). Named PrDiffAnalysis because the raw provider
/// shape already claims PrDiff (AgentSmith.Contracts.Providers.PrDiff).
/// </summary>
public sealed record PrDiffAnalysis(
    string BaseSha,
    string HeadSha,
    IReadOnlyList<PrDiffFile> Files);
