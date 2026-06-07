import { useMemo } from "react";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0175-fix: aggregate LLM cost over a rolling 24h / 7d window from the run
// snapshots. Previously the hook walked a runEvents array that /system never
// subscribed to (cost was always $0). Now we read `RunSnapshot.costUsd` — the
// projector rolls up LlmCallFinished into the per-run row, so the /system
// overview gets cost from the DB-backed run list (p0246f: fetched via
// GET /api/runs, refetched on the RunsChanged nudge).

export interface CostRollup {
  today: number;
  week: number;
  llmCalls: number;
}

const DAY_MS = 24 * 60 * 60 * 1000;
const WEEK_MS = 7 * DAY_MS;

export function useCostRollup(
  overview: OverviewSnapshot | null,
  now: Date = new Date(),
): CostRollup {
  return useMemo(
    () => deriveCostRollup(overview, now.getTime()),
    [overview, now.getTime()],
  );
}

export function deriveCostRollup(
  overview: OverviewSnapshot | null,
  nowMs: number,
): CostRollup {
  if (overview === null) return { today: 0, week: 0, llmCalls: 0 };

  let today = 0;
  let week = 0;
  let llmCalls = 0;
  const dayCutoff = nowMs - DAY_MS;
  const weekCutoff = nowMs - WEEK_MS;

  const accumulate = (run: RunSnapshot) => {
    const tsMs = run.finishedAt
      ? Date.parse(run.finishedAt)
      : Date.parse(run.startedAt);
    if (Number.isNaN(tsMs)) return;
    if (tsMs < weekCutoff) return;
    week += run.costUsd;
    llmCalls += run.llmCalls;
    if (tsMs >= dayCutoff) today += run.costUsd;
  };

  for (const run of overview.active) accumulate(run);
  for (const run of overview.recent) accumulate(run);

  return { today, week, llmCalls };
}
