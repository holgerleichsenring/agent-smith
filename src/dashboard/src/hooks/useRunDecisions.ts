"use client";

import { useEffect, useState } from "react";
import { fetchRunDecisions, type RunDecisionRow } from "@/lib/runStepsApi";

// p0388b: the run's latest logged decisions from the durable RunDecision
// projection. The Building beat's notes used to read them out of the client's
// live event buffer, which loses them the moment the run outgrows the window —
// exactly the runs whose decisions matter most.

export function useRunDecisions(runId: string | null, revision: unknown): RunDecisionRow[] {
  const [decisions, setDecisions] = useState<RunDecisionRow[]>([]);

  useEffect(() => {
    if (!runId) {
      setDecisions([]);
      return;
    }
    const ctrl = new AbortController();
    void (async () => {
      try {
        setDecisions(await fetchRunDecisions(runId, ctrl.signal));
      } catch {
        /* keep the last list rendered; the next tick refetches */
      }
    })();
    return () => ctrl.abort();
  }, [runId, revision]);

  return decisions;
}
