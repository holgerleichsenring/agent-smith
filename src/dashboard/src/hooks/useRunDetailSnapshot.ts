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

  if (detail && detail.runId === runId) return detail;
  return listSnapshot;
}
