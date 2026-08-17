"use client";

import { useEffect, useState } from "react";
import { fetchRunStatistics, type RunStatistics } from "@/lib/runStoryApi";

// p0423b: the story view's numbers, fetched ONCE when the operator opens the story.
// Diagnosis is a deliberate act — it does not poll, and it never rides the live surface.

export interface UseRunStatisticsResult {
  statistics: RunStatistics | null;
  loading: boolean;
  error: string | null;
}

export function useRunStatistics(runId: string | null): UseRunStatisticsResult {
  const [statistics, setStatistics] = useState<RunStatistics | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!runId) return;
    const ctrl = new AbortController();
    setLoading(true);
    setError(null);
    fetchRunStatistics(runId, ctrl.signal)
      .then((result) => {
        setStatistics(result);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (ctrl.signal.aborted) return;
        setError(err instanceof Error ? err.message : String(err));
        setLoading(false);
      });
    return () => ctrl.abort();
  }, [runId]);

  return { statistics, loading, error };
}
