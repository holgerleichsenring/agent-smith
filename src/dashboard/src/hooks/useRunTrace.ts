"use client";

import { useEffect, useState } from "react";
import { fetchRunTrace, type RunTraceEntryHeader } from "@/lib/runStoryApi";

// p0423b: the headers of a traced run's conversation. An untraced run has none, and that
// is an ABSENT reader, not a broken one — the list of what is readable never carries what
// is readable, because a recorded prompt reaches megabytes.

export interface UseRunTraceResult {
  entries: RunTraceEntryHeader[];
  loading: boolean;
}

export function useRunTrace(runId: string | null): UseRunTraceResult {
  const [entries, setEntries] = useState<RunTraceEntryHeader[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!runId) return;
    const ctrl = new AbortController();
    setLoading(true);
    fetchRunTrace(runId, ctrl.signal)
      .then((result) => {
        setEntries(result);
        setLoading(false);
      })
      .catch(() => {
        if (!ctrl.signal.aborted) setLoading(false);
      });
    return () => ctrl.abort();
  }, [runId]);

  return { entries, loading };
}
