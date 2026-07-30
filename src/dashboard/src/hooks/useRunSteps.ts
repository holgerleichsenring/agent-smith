"use client";

import { useEffect, useState } from "react";
import { fetchRunSteps, type RunStepRow } from "@/lib/runStepsApi";

// p0388b: the execution rail, queried from the DB projections. One row per step,
// so the payload and the client's memory are flat over the run's lifetime — the
// rail paints complete on the first fetch and stays complete after a reload,
// with no dependency on which events happen to be in the live buffer.
//
// The rail changes only when a step starts or finishes (tens of times per run),
// so it refetches on the run's snapshot ticks rather than on a timer of its own:
// `revision` is any value the caller already re-renders on (the run snapshot),
// which makes this hook free of its own polling loop.

export function useRunSteps(runId: string | null, revision: unknown): RunStepRow[] {
  const [steps, setSteps] = useState<RunStepRow[]>([]);

  useEffect(() => {
    if (!runId) {
      setSteps([]);
      return;
    }
    const ctrl = new AbortController();
    void (async () => {
      try {
        setSteps(await fetchRunSteps(runId, ctrl.signal));
      } catch {
        /* keep the last rail rendered; the next tick refetches */
      }
    })();
    return () => ctrl.abort();
  }, [runId, revision]);

  return steps;
}
