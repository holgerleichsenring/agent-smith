import { useMemo } from "react";
import {
  EventType,
  type LlmCallFinishedEvent,
  type RunEvent,
} from "@/types/hub-events";

// p0173d: aggregate LLM cost over a rolling 24h / 7d window.
// Source data comes from the RUN event channel (LlmCallFinished.costUsd)
// because cost is a per-run-step concept; the system stream doesn't carry it.
// Pure derivation against the recent run-event window the broadcaster
// already holds — operators see "what did we spend today".

export interface CostRollup {
  today: number;
  week: number;
  llmCalls: number;
}

const DAY_MS = 24 * 60 * 60 * 1000;
const WEEK_MS = 7 * DAY_MS;

export function useCostRollup(events: readonly RunEvent[], now: Date = new Date()): CostRollup {
  return useMemo(() => deriveCostRollup(events, now.getTime()), [events, now.getTime()]);
}

export function deriveCostRollup(
  events: readonly RunEvent[],
  nowMs: number,
): CostRollup {
  let today = 0;
  let week = 0;
  let llmCalls = 0;
  const dayCutoff = nowMs - DAY_MS;
  const weekCutoff = nowMs - WEEK_MS;

  for (const e of events) {
    if (e.type !== EventType.LlmCallFinished) continue;
    const ev = e as LlmCallFinishedEvent;
    const ts = Date.parse(ev.timestamp);
    if (ts < weekCutoff) continue;
    week += ev.costUsd;
    llmCalls++;
    if (ts >= dayCutoff) today += ev.costUsd;
  }

  return { today, week, llmCalls };
}
