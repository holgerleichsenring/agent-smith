namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for context compaction in the agentic loop.
/// Controls when and how old conversation history is summarized to prevent unbounded growth.
/// </summary>
public sealed class CompactionConfig
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// DEPRECATED no-op (p0357). The iteration branch never reset its counter, so past
    /// the threshold EVERY call compacted — per-iteration cache invalidation plus a
    /// summarizer call per iteration, and long runs spent their life in the amnesia
    /// regime. Token pressure (<see cref="MaxContextTokensTriggerRatio"/>) is the only
    /// trigger now. Still parsed so existing agentsmith.yml files load; a non-default
    /// value logs a deprecation warning at startup and is otherwise ignored.
    /// </summary>
    public int ThresholdIterations { get; set; } = DefaultThresholdIterations;

    /// <summary>The historical default — used only to detect an explicitly configured
    /// (and now ignored) value for the deprecation warning.</summary>
    public const int DefaultThresholdIterations = 8;

    /// <summary>
    /// p0357: default raised 80k → 200k — use the window the models actually have.
    /// 80k starved large multi-repo runs whose pinned head alone (skill + plan +
    /// principles + ledger + ticket) is a five-figure token count.
    /// </summary>
    public int MaxContextTokens { get; set; } = 200000;

    /// <summary>
    /// The compaction trigger: fires when accumulated input tokens exceed
    /// <c>ratio × <see cref="MaxContextTokens"/></c>. Default 0.7 leaves 30% headroom for the next
    /// response. Set to 0 (or any non-positive value) to disable compaction triggering
    /// entirely (p0357: there is no iteration fallback anymore).
    /// </summary>
    public double MaxContextTokensTriggerRatio { get; set; } = 0.7;

    /// <summary>The most recent messages a fold keeps verbatim: max(4, this × 2), fewer when the fold target needs it.</summary>
    public int KeepRecentIterations { get; set; } = 3;

    /// <summary>
    /// 2026-11-17-cb32a: a fold reduces the forwarded view to at most <c>ratio × <see cref="MaxContextTokens"/></c>,
    /// keeping fewer recent messages when they would not fit. The gap to the trigger is the growth that passes before
    /// the next fold. A fold that kept three rounds whatever their size left the view over the trigger when a round
    /// carried a large tool result, so the next call folded again: 14 of 50 executor calls in the replay of
    /// 2026-11-17, each one rewriting the history the provider had cached. Keep it below
    /// <see cref="MaxContextTokensTriggerRatio"/>; at or above it, every call past the trigger folds.
    /// </summary>
    public double FoldTargetRatio { get; set; } = 0.35;

    /// <summary>
    /// 2026-11-17-cb32a: each fold appends its summary as a segment after the earlier ones, so what came before the
    /// newest fold is not rewritten. Segments that together would pass <c>ratio × <see cref="MaxContextTokens"/></c>
    /// are summarized into one at that fold, which keeps the summary inside the window on a run of any length.
    /// </summary>
    public double SummaryBudgetRatio { get; set; } = 0.1;
}
