"use client";

import { useEffect, useState } from "react";
import { fetchRunPhases, type RunPhaseRow } from "@/lib/runPhasesApi";

// p0466: the run's phases from the durable RunPhase projection. A run with no
// derived phases returns an empty list, and the Building beat then looks exactly
// as it did before — no empty segment standing in for work that never happened.

export function useRunPhases(runId: string | null, revision: unknown): RunPhaseRow[] {
  const [phases, setPhases] = useState<RunPhaseRow[]>([]);

  useEffect(() => {
    if (!runId) {
      setPhases([]);
      return;
    }
    const ctrl = new AbortController();
    void (async () => {
      try {
        setPhases(await fetchRunPhases(runId, ctrl.signal));
      } catch {
        /* keep the last list rendered; the next tick refetches */
      }
    })();
    return () => ctrl.abort();
  }, [runId, revision]);

  return phases;
}
