"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";

// p0235: pulls the run's plan.md from JobsHub.GetPlanMarkdown. For coding
// presets this is the agent's own plan (read back from the sandbox at
// run-finish). Returns null when the cache is cold (>24h), the run is unknown,
// or no plan was written — the dashboard then hides the plan panel.

export interface UsePlanMarkdownResult {
  content: string | null;
  loading: boolean;
  error: string | null;
}

export function usePlanMarkdown(runId: string | null): UsePlanMarkdownResult {
  const { client } = useJobsHub();
  const [content, setContent] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!runId) {
      setContent(null);
      setError(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    client.getPlanMarkdown(runId)
      .then((result) => {
        if (!cancelled) {
          setContent(result);
          setLoading(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : String(err));
          setLoading(false);
        }
      });
    return () => { cancelled = true; };
  }, [client, runId]);

  return { content, loading, error };
}
