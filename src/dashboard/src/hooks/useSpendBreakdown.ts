import { useMemo } from "react";
import { WEEK_MS, costWindowTimestampMs } from "@/hooks/useCostRollup";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// 2026-08-27-7463: WHERE the money went, grouped out of the run list the
// dashboard already holds. No endpoint, no server-side aggregation: the same
// snapshots the headline sums are bucketed by the work they were spent on.
//
// The window, the timestamp choice and the iteration order are the headline's
// (active then recent, undeduplicated), so the slices sum to the 7-day figure
// exactly rather than approximately.
//
// The work dimension is the run's REPOS, not its project: RunSnapshot carries
// no project — the rail says so where it declines to draw a Projects section —
// and a project inferred from a repo would be a guess printed as a fact. A
// multi-repo run is one slice under its whole repo set, so no run's cost is
// counted twice.

const UNATTRIBUTED_WORK = "unattributed";
const UNKNOWN_PIPELINE = "unknown pipeline";

export interface SpendSlice {
  /** Grouping identity — stable across renders, usable as a React key. */
  key: string;
  /** The repos the spend was booked against, as one line. */
  work: string;
  pipeline: string;
  amountUsd: number;
  /** The slice's share of the total, 0..1. Zero when nothing was spent. */
  share: number;
}

export function useSpendBreakdown(
  overview: OverviewSnapshot | null,
  now: Date = new Date(),
): SpendSlice[] {
  return useMemo(
    () => deriveSpendBreakdown(overview, now.getTime()),
    [overview, now.getTime()],
  );
}

export function deriveSpendBreakdown(
  overview: OverviewSnapshot | null,
  nowMs: number,
): SpendSlice[] {
  if (overview === null) return [];
  const slices = new Map<string, SpendSlice>();
  const cutoff = nowMs - WEEK_MS;
  for (const run of [...overview.active, ...overview.recent]) {
    const tsMs = costWindowTimestampMs(run);
    if (Number.isNaN(tsMs) || tsMs < cutoff) continue;
    book(slices, run);
  }
  return rankedSlices([...slices.values()]);
}

function book(slices: Map<string, SpendSlice>, run: RunSnapshot): void {
  const repos = run.repos?.filter((repo) => repo.length > 0) ?? [];
  const work = repos.length > 0 ? [...repos].sort().join(" + ") : UNATTRIBUTED_WORK;
  const pipeline = run.pipeline || UNKNOWN_PIPELINE;
  const key = `${work} · ${pipeline}`;
  const booked = slices.get(key) ?? { key, work, pipeline, amountUsd: 0, share: 0 };
  booked.amountUsd += run.costUsd;
  slices.set(key, booked);
}

// Biggest spend first — the question is where the money went, and the answer is
// read from the top. A slice that cost nothing is not an answer to it.
function rankedSlices(slices: SpendSlice[]): SpendSlice[] {
  const spent = slices.filter((slice) => slice.amountUsd > 0);
  const total = spent.reduce((sum, slice) => sum + slice.amountUsd, 0);
  for (const slice of spent) slice.share = total > 0 ? slice.amountUsd / total : 0;
  return spent.sort((a, b) => b.amountUsd - a.amountUsd);
}
