"use client";

import { useEffect, useState } from "react";
import { fetchRunPhase } from "@/lib/runPhasesApi";

// p0466: the spec a phase executed, fetched when the operator opens that phase.
// It is the largest thing a phase carries, so the list read never ships it —
// opening one phase costs one document, not every document the run produced.

export function useRunPhaseRecord(
  runId: string | null,
  phaseId: string | null,
): { record: string | null; loading: boolean } {
  const [record, setRecord] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!runId || !phaseId) {
      setRecord(null);
      return;
    }
    const ctrl = new AbortController();
    setLoading(true);
    void (async () => {
      try {
        const detail = await fetchRunPhase(runId, phaseId, ctrl.signal);
        setRecord(detail?.record ?? null);
      } catch {
        setRecord(null);
      } finally {
        setLoading(false);
      }
    })();
    return () => ctrl.abort();
  }, [runId, phaseId]);

  return { record, loading };
}
