"use client";

import { useAttemptMachines, useTicketDetail, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketSeries } from "@/types/tickets";
import { ComputeTable, ComputeTiles, WaitingBanner } from "./ComputeSection";
import { computeTotals, duration, type AttemptMachine } from "./computeTotals";
import { CostSection } from "./CostSection";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59s: the Cost & compute tab. Every figure is a field one backend member delivers — the
// attempt's cost (9a59a), the run's cap, LLM calls and reserved Gi-minutes (run detail), the passes split
// (9a59f), the spawns (9a59i) and the segments with their parks (9a59j); nothing is summed from a paged list
// or estimated. Attempts read oldest first, the order their bars were spent in.

/** Spec id -> ordinal, from every attempt's executed specs and the ticket's default cut. */
function ordinalsOf(attempts: TicketAttempt[], series: TicketSeries | null): Record<string, number> {
  const ordinals: Record<string, number> = {};
  for (const spec of series?.specs ?? []) ordinals[spec.specId] = spec.ordinal;
  for (const attempt of attempts) for (const spec of attempt.executedSpecs) ordinals[spec.specId] ??= spec.ordinal;
  return ordinals;
}

export function TicketCostTab() {
  const detail = useTicketDetail();
  const series = useTicketSeries();
  const address = useTicketTabGo();
  const attempts = [...(detail.data?.attempts ?? [])].sort((l, r) => (l.runId < r.runId ? -1 : l.runId > r.runId ? 1 : 0));
  const reads = useAttemptMachines(attempts.map((a) => a.runId));
  const now = Date.now();
  const machines: AttemptMachine[] = attempts.map((attempt, i) => ({
    attempt,
    compute: reads.compute[attempt.runId]?.data ?? null,
    run: reads.run[attempt.runId]?.data ?? null,
    next: attempts[i + 1] ?? null,
  }));
  const totals = computeTotals(machines, now);

  return (
    <>
      <CostSection attempts={attempts} derivations={detail.data?.derivations ?? []} machines={reads}
        ordinals={ordinalsOf(attempts, series.data)} />
      <div className="section-head">
        <h2>Compute</h2><span className="cnt" data-testid="compute-pod-time">{duration(totals.podMinutes * 60000)} pod time</span>
        <span className="sh-sub">every counted sandbox of every attempt · bar = wall clock, one scale</span>
      </div>
      <ComputeTiles totals={totals} />
      <WaitingBanner totals={totals} onRead={() => address.go("conversation", { [CAUSE_PARAM]: null })} />
      <ComputeTable machines={machines} now={now} />
    </>
  );
}
