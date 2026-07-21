"use client";

import { useEffect, useRef, useState } from "react";
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
//
// p0359: requests are SERIALIZED, never cancelled by a newer list tick. The
// previous implementation aborted the in-flight fetch on every tick; under a
// busy run the list refreshes every ~350ms, so as soon as the detail request
// took longer than one tick EVERY request was cancelled before completing —
// a wall of (canceled) network calls and story surfaces that never update
// while the run is at its busiest. Now a tick that lands mid-flight marks
// the join dirty and ONE trailing fetch runs after the current one settles.
// Abort still happens where it belongs: unmount and runId change.
export function useRunDetailSnapshot(
  runId: string,
  listSnapshot: RunSnapshot | null,
): RunSnapshot | null {
  const [detail, setDetail] = useState<RunSnapshot | null>(null);
  const alive = useRef<string | null>(null);
  const inFlight = useRef(false);
  const dirty = useRef(false);
  const ctrl = useRef<AbortController | null>(null);

  useEffect(() => {
    alive.current = runId;
    return () => {
      alive.current = null;
      ctrl.current?.abort();
      ctrl.current = null;
    };
  }, [runId]);

  useEffect(() => {
    if (inFlight.current) {
      dirty.current = true;
      return;
    }
    inFlight.current = true;
    void (async () => {
      try {
        do {
          dirty.current = false;
          const id = alive.current;
          if (!id) return;
          const c = new AbortController();
          ctrl.current = c;
          try {
            const d = await fetchRun(id, c.signal);
            if (alive.current === id && d) setDetail(d);
          } catch {
            /* the list row keeps rendering; the next tick retries */
          }
        } while (alive.current !== null && dirty.current);
      } finally {
        inFlight.current = false;
      }
    })();
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
