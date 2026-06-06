"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";

// p0243: pulls the run's analyze.md from JobsHub.GetAnalyzeMarkdown — the
// analyzer's ProjectMap rendered as markdown (language, build/test commands,
// modules, test projects). Returns null when the cache is cold (>24h), the run
// is unknown, or no analysis was cached — the dashboard then hides the panel.

export interface UseAnalyzeMarkdownResult {
  content: string | null;
  loading: boolean;
  error: string | null;
}

export function useAnalyzeMarkdown(runId: string | null): UseAnalyzeMarkdownResult {
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
    client.getAnalyzeMarkdown(runId)
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
