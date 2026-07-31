"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";

// p0390: pulls the run's work spec from JobsHub.GetSpecMarkdown — the current
// revision plus the revision list, each naming its cause. Returns null when the
// run derived no spec (a preset without DeriveSpecification, a ticketless run,
// or a cold cache); the Plan beat then shows only the plan.

export interface UseSpecMarkdownResult {
  content: string | null;
  loading: boolean;
  error: string | null;
}

export function useSpecMarkdown(runId: string | null): UseSpecMarkdownResult {
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
    client.getSpecMarkdown(runId)
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
