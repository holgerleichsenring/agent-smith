"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchRunStepEvents } from "@/lib/runStepsApi";
import type { RunEvent } from "@/types/hub-events";

// p0388b: ONE step's structural events, fetched when that step is selected and
// extended page by page through the Seq cursor. Selecting a step never fetches
// the run's whole trail, and deselecting it drops the page — so the detail
// pane's cost is bounded by what the operator is actually looking at.

export interface RunStepEvents {
  events: RunEvent[];
  hasMore: boolean;
  loading: boolean;
  loadMore: () => void;
}

const EMPTY: RunEvent[] = [];

export function useRunStepEvents(runId: string | null, stepIndex: number | null): RunStepEvents {
  const [events, setEvents] = useState<RunEvent[]>(EMPTY);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const cursor = useRef(0);

  const load = useCallback(
    async (sinceSeq: number, signal?: AbortSignal) => {
      if (!runId || stepIndex === null) return;
      setLoading(true);
      try {
        const page = await fetchRunStepEvents(runId, stepIndex, sinceSeq, signal);
        cursor.current = page.nextSeq;
        setHasMore(page.hasMore);
        setEvents((prev) => (sinceSeq === 0 ? page.events : [...prev, ...page.events]));
      } catch {
        /* the pane keeps what it has; the operator can retry via load more */
      } finally {
        setLoading(false);
      }
    },
    [runId, stepIndex],
  );

  useEffect(() => {
    cursor.current = 0;
    setEvents(EMPTY);
    setHasMore(false);
    if (!runId || stepIndex === null) return;
    const ctrl = new AbortController();
    void load(0, ctrl.signal);
    return () => ctrl.abort();
  }, [runId, stepIndex, load]);

  const loadMore = useCallback(() => void load(cursor.current), [load]);

  return { events, hasMore, loading, loadMore };
}
