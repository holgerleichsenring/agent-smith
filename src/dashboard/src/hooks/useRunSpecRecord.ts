"use client";

import { useEffect, useState } from "react";
import { fetchRunSpec } from "@/lib/runSpecsApi";

// p0466: the spec body a run executed, fetched when the operator opens that spec.
// It is the largest thing a spec row carries, so the list read never ships it —
// opening one spec costs one document, not every document the run produced.

export function useRunSpecRecord(
  runId: string | null,
  specId: string | null,
): { record: string | null; loading: boolean } {
  const [record, setRecord] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!runId || !specId) {
      setRecord(null);
      return;
    }
    const ctrl = new AbortController();
    setLoading(true);
    void (async () => {
      try {
        const detail = await fetchRunSpec(runId, specId, ctrl.signal);
        setRecord(detail?.record ?? null);
      } catch {
        setRecord(null);
      } finally {
        setLoading(false);
      }
    })();
    return () => ctrl.abort();
  }, [runId, specId]);

  return { record, loading };
}
