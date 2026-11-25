import { useMemo } from "react";
import type { RunSpend, RunSpendSlice } from "@/lib/runSpendApi";

// 2026-08-27-7463: WHERE the money went, by the work it was spent on. 2026-11-24-0fbfb: grouped by the server in the
// week's spend rollup (GET /api/runs/spend), the same read the headline sums, so the slices still add up to the 7-day
// figure — over every run of the caller's projects, not the overview's window.
//
// The work dimension is the run's REPOS, not its project; a multi-repo run is one slice under its whole repo set, so
// no run's cost is counted twice.

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

export function useSpendBreakdown(week: RunSpend | null): SpendSlice[] {
  return useMemo(() => deriveSpendBreakdown(week), [week]);
}

export function deriveSpendBreakdown(week: RunSpend | null): SpendSlice[] {
  if (week === null) return [];
  const slices = new Map<string, SpendSlice>();
  for (const slice of week.slices) book(slices, slice);
  return rankedSlices([...slices.values()]);
}

function book(slices: Map<string, SpendSlice>, slice: RunSpendSlice): void {
  const repos = slice.repos.filter((repo) => repo.length > 0);
  const work = repos.length > 0 ? [...repos].sort().join(" + ") : UNATTRIBUTED_WORK;
  const pipeline = slice.pipeline || UNKNOWN_PIPELINE;
  const key = `${work} · ${pipeline}`;
  const booked = slices.get(key) ?? { key, work, pipeline, amountUsd: 0, share: 0 };
  booked.amountUsd += slice.costUsd;
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
