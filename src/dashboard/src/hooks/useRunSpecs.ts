"use client";

import { useEffect, useState } from "react";
import { fetchRunSpecs, type RunSpecRow } from "@/lib/runSpecsApi";

// p0466: the run's specs from the durable RunSpec projection. A run that executed
// no spec returns an empty list, and the Building beat then looks exactly as it
// did before — no empty segment standing in for work that never happened.

export function useRunSpecs(runId: string | null, revision: unknown): RunSpecRow[] {
  const [specs, setSpecs] = useState<RunSpecRow[]>([]);

  useEffect(() => {
    if (!runId) {
      setSpecs([]);
      return;
    }
    const ctrl = new AbortController();
    void (async () => {
      try {
        setSpecs(await fetchRunSpecs(runId, ctrl.signal));
      } catch {
        /* keep the last list rendered; the next tick refetches */
      }
    })();
    return () => ctrl.abort();
  }, [runId, revision]);

  return specs;
}
