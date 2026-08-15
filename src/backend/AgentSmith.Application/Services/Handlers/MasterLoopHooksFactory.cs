using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Assembles the open-loop governor hooks — the within-pass money fence, the
/// per-iteration usage recording, the ledger reminder and the compaction pin
/// carriers (p0341c/d/e). Pure over values; p0411 lifted it out of the handler.
/// </summary>
internal static class MasterLoopHooksFactory
{
    // The fence uses an independent per-pass estimator seeded from the master's
    // start-of-loop spend, so it stays a clean signal separate from the shared
    // tracker (which the handler updates between passes for result.md accuracy).
    internal static MasterLoopHooks Build(
        AgenticMasterContext context, PipelineCostTracker costTracker, Func<ProgressLedger> ledger,
        LogDecisionToolHost log)
    {
        context.Pipeline.TryGet<IModelPricingResolver>("ModelPricingResolver", out var resolver);
        context.Pipeline.TryGet<PricingConfig>("ProjectPricing", out var pricingConfig);
        var cap = context.Pipeline.TryGet<CostCapValues>("PipelineCostCap", out var c) ? c : null;
        var estimator = new PipelineCostTracker(resolver, pricingConfig, null);
        var startUsd = costTracker.EstimateCostUsd();
        // p0376: use the cache-weighted token total, NOT raw TotalTokens — otherwise this
        // fence trips on cache-read volume (which is nearly free) while the USD cap has
        // ample room, killing a run mid-pass. Mirrors PipelineCostTracker.IsBudgetExhausted.
        var startTokens = costTracker.EffectiveBudgetTokens;
        return new MasterLoopHooks(
            IsBudgetExhausted: cap is null
                ? null
                : () => startUsd + estimator.EstimateCostUsd() > cap.Usd
                    || startTokens + estimator.EffectiveBudgetTokens > cap.Tokens,
            // p0341e: record EACH tool-loop iteration's usage into BOTH the pass-local fence
            // estimator AND the shared per-pipeline tracker — as it happens. This is the fix
            // for the run summary that showed $0.14 while the master truly spent $16.38: the
            // handler previously fed the shared tracker ONLY the FunctionInvokingChatClient's
            // final aggregate via Track(loopResult.Response) AFTER the loop, so a pass that
            // ended by THROWING (the within-pass money fence, or an LLM-layer timeout) dropped
            // its ENTIRE spend from the summary and from IsBudgetExhausted. Feeding per
            // iteration makes the shared tracker exact and exception-proof; the redundant
            // handler-level Track calls for the coding master are dropped to avoid double-count.
            // The fence math is unaffected — it reads the FROZEN startUsd/startTokens plus the
            // independent estimator, never the shared tracker live.
            RecordIterationUsage: response =>
            {
                estimator.Track(response);
                costTracker.Track(response);
            },
            RenderReminder: () => MasterNudges.BuildInPassReminder(ledger()),
            ReminderEveryNIterations: context.AgentConfig.LedgerReminderEveryNIterations,
            DriftEditlessIterations: context.AgentConfig.ReminderDriftEditlessIterations,
            // p0341d: the compaction PIN carriers — rendered CURRENT from PipelineContext /
            // the live decision log at compaction time, never a pass-start snapshot. So the
            // continuous pass preserves the THREAD (ledger + working state) as it compacts.
            RenderLedgerForPin: () =>
            {
                var l = ledger();
                return l.IsEmpty ? null : ProgressLedgerRenderer.Render(l);
            },
            RenderWorkingStateForPin: () => WorkingStateSection.Build(log.GetDecisions(), null),
            Compaction: context.AgentConfig.Compaction);
    }
}
