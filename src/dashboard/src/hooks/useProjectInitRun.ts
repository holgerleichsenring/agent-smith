"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import { fetchProjectInit, type LiveInitRun } from "@/lib/projectInitApi";

// 2026-10-02-5f89d: the project's live init run, read from the server — on mount, so the
// Initialize button survives a navigation, and on the jobs hub's RunsChanged nudge, so it
// follows the run to its end. The nudge fires per run event; only a nudge for the live
// run or for a run id this hook has not seen yet can change the answer, so the rest are
// ignored rather than turned into a request per event per project card.

const NUDGE_COALESCE_MS = 350;

export interface ProjectInitRun {
  live: LiveInitRun | null;
  /** Re-read now. Resolves true when the server answered, false when it could not —
   *  no read permission, no server — and the last answer stays. */
  refresh: () => Promise<boolean>;
}

export function useProjectInitRun(project: string): ProjectInitRun {
  const client = useMemo(() => getJobsHubClient(HUB_URL), []);
  const [live, setLive] = useState<LiveInitRun | null>(null);
  const liveRunId = useRef<string | null>(null);
  const seen = useRef(new Set<string>());
  const inFlight = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    inFlight.current?.abort();
    const ctrl = new AbortController();
    inFlight.current = ctrl;
    try {
      const run = await fetchProjectInit(project, ctrl.signal);
      if (ctrl.signal.aborted) return false;
      liveRunId.current = run?.runId ?? null;
      setLive(run);
      return true;
    } catch {
      return false;
    }
  }, [project]);

  useEffect(() => {
    void refresh();
    return () => inFlight.current?.abort();
  }, [refresh]);

  useEffect(() => {
    // A live run nudges many times a second; one re-read per window is enough.
    let timer: ReturnType<typeof setTimeout> | null = null;
    const off = client.runsChanged.add((runId) => {
      if (runId !== liveRunId.current && seen.current.has(runId)) return;
      seen.current.add(runId);
      if (timer !== null) return;
      timer = setTimeout(() => {
        timer = null;
        void refresh();
      }, NUDGE_COALESCE_MS);
    });
    return () => {
      if (timer !== null) clearTimeout(timer);
      off();
    };
  }, [client, refresh]);

  return { live, refresh };
}
