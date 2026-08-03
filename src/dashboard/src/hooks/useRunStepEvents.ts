"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchRunStepEventDelta, fetchRunStepEventPage } from "@/lib/runStepsApi";
import type { RunEvent } from "@/types/hub-events";

// p0388b: ONE step's structural events, fetched when that step is selected and
// paged through a Seq cursor. Selecting a step never fetches the run's whole
// trail, and deselecting it drops the page — so the detail pane's cost is
// bounded by what the operator is actually looking at.
//
// p0388d: and it FOLLOWS the run. The p0388b hook read once on mount, from the
// oldest row forward, which produced the symptom the operator reported: the
// counter on the left climbs while the pane on the right shows nothing new,
// even after a reload. Two things changed. Opening a step now lands on its
// NEWEST page, because the end of a long step is what someone opens it for.
// And while the run is live the hook polls forward from its newest cursor, so
// the pane keeps appending on its own. Going back into the step's history is a
// separate, explicit walk — never an infinite scroll that quietly re-fetches.

/** The forward poll's rest interval — the gap between ticks, not a limit on
 *  what a tick reads: a tick that finds more than one page waiting drains the
 *  rest before resting, so the pane never trails a busy step by a page a
 *  second. */
export const STEP_POLL_INTERVAL_MS = 1000;

export interface RunStepEvents {
  events: RunEvent[];
  /** Older rows exist for this step than the pane is showing. The view states
   *  this rather than rendering a page that looks like the whole step. */
  hasOlder: boolean;
  loading: boolean;
  loadOlder: () => void;
}

const EMPTY: RunEvent[] = [];

export function useRunStepEvents(
  runId: string | null,
  stepIndex: number | null,
  live: boolean,
): RunStepEvents {
  const [events, setEvents] = useState<RunEvent[]>(EMPTY);
  const [hasOlder, setHasOlder] = useState(false);
  const [loading, setLoading] = useState(false);
  const newest = useRef(0);
  const oldest = useRef(0);

  // The newest page. Also what the poll falls back to while the step has not
  // produced a single row yet: there is no cursor to read forward from, and
  // asking for "everything from the start" is the anchor bug this phase removed.
  const readNewestPage = useCallback(
    async (signal?: AbortSignal) => {
      if (!runId || stepIndex === null) return;
      const page = await fetchRunStepEventPage(runId, stepIndex, null, signal);
      if (signal?.aborted) return;
      newest.current = page.newestSeq;
      oldest.current = page.oldestSeq;
      setEvents(page.events);
      setHasOlder(page.hasOlder);
    },
    [runId, stepIndex],
  );

  useEffect(() => {
    newest.current = 0;
    oldest.current = 0;
    setEvents(EMPTY);
    setHasOlder(false);
    if (!runId || stepIndex === null) return;
    const ctrl = new AbortController();
    void (async () => {
      setLoading(true);
      try {
        await readNewestPage(ctrl.signal);
      } catch {
        /* the pane stays empty; the poll or a re-selection reads again */
      } finally {
        if (!ctrl.signal.aborted) setLoading(false);
      }
    })();
    return () => ctrl.abort();
  }, [runId, stepIndex, readNewestPage]);

  // The forward poll, bounded by the RUN's own liveness — a finished run costs
  // nothing, because this effect never starts (and tears itself down when the
  // run reaches a terminal status while the pane is open).
  useEffect(() => {
    if (!live || !runId || stepIndex === null) return;
    const ctrl = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;

    const tick = async () => {
      if (ctrl.signal.aborted) return;
      try {
        if (newest.current === 0) {
          await readNewestPage(ctrl.signal);
        } else {
          // Drain: a step that produced more than one page between ticks is
          // read to its end NOW, rather than trailing the run by a page per
          // second. The loop ends when the server says nothing newer is left,
          // so what bounds it is the step's own backlog.
          let more = true;
          while (more && !ctrl.signal.aborted) {
            const delta = await fetchRunStepEventDelta(
              runId, stepIndex, newest.current, ctrl.signal);
            if (ctrl.signal.aborted) return;
            if (delta.events.length > 0) setEvents((prev) => [...prev, ...delta.events]);
            // The CURSOR decides whether to read on, not the event count. The
            // server moves it past every row it scanned, so a page whose
            // payloads it could not deserialize still advances the drain
            // instead of wedging it re-reading the same rows.
            const advanced = delta.newestSeq > newest.current;
            if (advanced) newest.current = delta.newestSeq;
            more = delta.hasNewer && advanced;
          }
        }
      } catch {
        /* a failed tick changes nothing — the next one retries */
      }
      if (!ctrl.signal.aborted) timer = setTimeout(() => void tick(), STEP_POLL_INTERVAL_MS);
    };

    timer = setTimeout(() => void tick(), STEP_POLL_INTERVAL_MS);
    return () => {
      ctrl.abort();
      if (timer) clearTimeout(timer);
    };
  }, [live, runId, stepIndex, readNewestPage]);

  // History, walked explicitly: one page below the oldest row on screen, and
  // contiguous with it, so the walk back has no gaps.
  const loadOlder = useCallback(() => {
    if (!runId || stepIndex === null || !hasOlder) return;
    void (async () => {
      setLoading(true);
      try {
        const page = await fetchRunStepEventPage(runId, stepIndex, oldest.current);
        oldest.current = page.oldestSeq;
        setEvents((prev) => [...page.events, ...prev]);
        setHasOlder(page.hasOlder);
      } catch {
        /* the pane keeps what it has; the operator can ask again */
      } finally {
        setLoading(false);
      }
    })();
  }, [runId, stepIndex, hasOlder]);

  return { events, hasOlder, loading, loadOlder };
}
