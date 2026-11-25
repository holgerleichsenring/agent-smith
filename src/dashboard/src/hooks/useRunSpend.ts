"use client";

import { useEffect, useState } from "react";
import { fetchRunSpend, localDayStart, weekStart, type RunSpend } from "@/lib/runSpendApi";
import type { OverviewSnapshot } from "@/types/hub-events";

// 2026-11-24-0fbfb: the spend rollup since the viewer's local midnight ("today") or since seven days ago ("week"),
// read again whenever `refresh` changes — spendKey of the run overview, which changes when a run starts or its cost
// moves, not on every overview push. The instant is taken when the read starts, so a page left open past midnight
// counts the new day on its next read.

export type SpendWindow = "today" | "week";

export interface RunSpendRead {
  spend: RunSpend | null;
  failed: boolean;
}

/** What the spend can have changed with: the runs the overview holds and what they have cost so far. Every live run
 *  is in the overview, so no spend moves without this changing. */
export function spendKey(overview: OverviewSnapshot | null): string {
  if (!overview) return "";
  const runs = [...overview.active, ...overview.recent];
  const cost = runs.reduce((sum, run) => sum + (run.costUsd ?? 0), 0);
  return `${overview.active.map((r) => r.runId).join(",")}|${overview.recent[0]?.runId ?? ""}|${runs.length}|${cost.toFixed(6)}`;
}

export function spendFrom(window: SpendWindow, nowMs: number): string {
  return window === "today" ? localDayStart(nowMs) : weekStart(nowMs);
}

export function useRunSpend(window: SpendWindow, refresh: unknown): RunSpendRead {
  const [read, setRead] = useState<RunSpendRead>({ spend: null, failed: false });
  useEffect(() => {
    const controller = new AbortController();
    fetchRunSpend(spendFrom(window, Date.now()), controller.signal)
      .then((spend) => setRead({ spend, failed: false }))
      .catch((e: Error) => {
        if (e.name !== "AbortError") setRead((prev) => ({ spend: prev.spend, failed: prev.spend === null }));
      });
    return () => controller.abort();
  }, [window, refresh]);
  return read;
}
