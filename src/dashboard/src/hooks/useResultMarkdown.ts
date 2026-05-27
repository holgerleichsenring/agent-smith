"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";

// p0169j-c: pulls the rendered result.md from JobsHub.GetResultMarkdown.
// Returns null when the cache is cold (>24h), the run is unknown, or
// WriteRunResult hasn't fired yet for an in-flight run. Dashboard
// renders a PR-link fallback in that case.

export interface UseResultMarkdownResult {
  content: string | null;
  loading: boolean;
  error: string | null;
}

export function useResultMarkdown(runId: string | null): UseResultMarkdownResult {
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
    client.getResultMarkdown(runId)
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
