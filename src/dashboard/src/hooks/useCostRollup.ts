import { useRunSpend, type RunSpendRead } from "@/hooks/useRunSpend";
import type { RunSpend } from "@/lib/runSpendApi";

// p0175-fix: the Overview's spend figures. 2026-11-24-0fbfb: read from the server's spend rollup (GET /api/runs/spend)
// over every run of the caller's projects — today since the viewer's local midnight, the week and its llm calls since
// seven days ago — no longer summed over the overview's bounded window of runs.

export interface CostRollup {
  today: number;
  week: number;
  llmCalls: number;
}

/** The figures, or null until both reads have answered. */
export function deriveCostRollup(today: RunSpendRead, week: RunSpendRead): CostRollup | null {
  if (!today.spend || !week.spend) return null;
  return { today: today.spend.totalUsd, week: week.spend.totalUsd, llmCalls: week.spend.llmCalls };
}

export interface SpendReading {
  cost: CostRollup | null;
  /** The week's rollup, which the breakdown groups. */
  week: RunSpend | null;
  /** A read failed with nothing read before it. */
  failed: boolean;
}

/** The Overview's two spend reads, made once for the card and the breakdown; `refresh` is spendKey of the overview. */
export function useCostRollup(refresh: unknown): SpendReading {
  const today = useRunSpend("today", refresh);
  const week = useRunSpend("week", refresh);
  return { cost: deriveCostRollup(today, week), week: week.spend, failed: today.failed || week.failed };
}
