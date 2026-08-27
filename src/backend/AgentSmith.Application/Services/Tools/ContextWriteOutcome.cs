namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-167c: what write_context_yaml did in THIS round.
/// <para>
/// The bootstrap round used to ask the sandbox whether context.yaml is there. On a
/// first init that is the same question. On a RE-INIT — the supported route back in
/// — the file is already on disk from last time, so five refusals still resolved to
/// success: the round reported green and the stale context survived untouched.
/// </para>
/// </summary>
/// <param name="Written">This round's write reached the sandbox and succeeded.</param>
/// <param name="LastRefusal">
/// What the tool said the last time it refused, defect included. Null together with
/// <paramref name="Written"/> false means the tool was never called at all.
/// </param>
/// <param name="BudgetExhausted">
/// The per-round refusal budget ran out, so the tool stopped inviting another attempt.
/// </param>
public sealed record ContextWriteOutcome(
    bool Written, string? LastRefusal, bool BudgetExhausted);
