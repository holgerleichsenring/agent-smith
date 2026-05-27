"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";
import type { RunEvent } from "@/types/hub-events";

// p0169h: pulls the full retained trail window via JobsHub.GetTrail.
// No pagination today — the 10000-event cap server-side fits in one fetch.

export interface UseTrailResult {
  events: RunEvent[];
  loading: boolean;
  error: string | null;
}

export function useTrail(runId: string | null): UseTrailResult {
  const { client } = useJobsHub();
  const [events, setEvents] = useState<RunEvent[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!runId) {
      setEvents([]);
      setError(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    client.getTrail(runId)
      .then((trail) => {
        if (!cancelled) {
          setEvents(trail);
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

  return { events, loading, error };
}
