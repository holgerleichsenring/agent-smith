"use client";

import { useEffect, useState } from "react";
import { fetchRun } from "@/lib/runsApi";
import type { RunSnapshot } from "@/types/hub-events";

// p0344b: the run DETAIL serves fields the list deliberately omits
// (progressLedger, acceptance, pendingQuestion, footprint — payload-heavy,
// per-run). The detail page's snapshot used to come from the overview LIST
// only, so those fields never reached the story surfaces. This hook joins
// them: fetch GET /api/runs/{id} and let the detail row win over the list
// row. Refetches whenever the list snapshot reference changes — the list
// updates at the nudge-coalesced cadence, so the join stays current without
// its own polling loop. While the detail is in flight the list row renders
// (progressive, never blank).
export function useRunDetailSnapshot(
  runId: string,
  listSnapshot: RunSnapshot | null,
): RunSnapshot | null {
  const [detail, setDetail] = useState<RunSnapshot | null>(null);

  useEffect(() => {
    let cancelled = false;
    const ctrl = new AbortController();
    void (async () => {
      try {
        const d = await fetchRun(runId, ctrl.signal);
        if (!cancelled && d) setDetail(d);
      } catch {
        /* the list row keeps rendering; the next nudge retries */
      }
    })();
    return () => {
      cancelled = true;
      ctrl.abort();
    };
  }, [runId, listSnapshot]);

  if (detail && detail.runId === runId) return reconcileCost(detail, listSnapshot);
  return listSnapshot;
}

// p0355: cost + llmCalls are the run's PERSISTED, monotonically non-decreasing
// totals — read straight off the snapshot, never re-summed from a partial live
// event buffer (that produced the "$0.03 on a run that cost far more" defect on
// revisit). The detail fetch is authoritative, but guard against a transient
// lower value by never showing less than the list snapshot already knew for the
// SAME finished run.
function reconcileCost(detail: RunSnapshot, list: RunSnapshot | null): RunSnapshot {
  if (!list || list.runId !== detail.runId) return detail;
  if (detail.costUsd >= list.costUsd && detail.llmCalls >= list.llmCalls) return detail;
  return {
    ...detail,
    costUsd: Math.max(detail.costUsd, list.costUsd),
    llmCalls: Math.max(detail.llmCalls, list.llmCalls),
  };
}
