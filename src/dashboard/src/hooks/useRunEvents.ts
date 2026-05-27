"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";
import type { RunEvent } from "@/types/hub-events";

// p0169f: per-run event log. Each subscriber appends to a local array;
// the JobsHubClient's ref-counted SubscribeRun ensures only one hub
// invocation per runId regardless of how many components mount.

const MAX_EVENTS_PER_RUN = 2000;

export function useRunEvents(runId: string | null): RunEvent[] {
  const { client } = useJobsHub();
  const [events, setEvents] = useState<RunEvent[]>([]);

  useEffect(() => {
    if (!runId) return;
    setEvents([]);
    const off = client.runEvents.add(({ runId: emittedRunId, event }) => {
      if (emittedRunId !== runId) return;
      setEvents((prev) => {
        const next = [...prev, event];
        return next.length > MAX_EVENTS_PER_RUN
          ? next.slice(next.length - MAX_EVENTS_PER_RUN)
          : next;
      });
    });
    let cancel: (() => Promise<void>) | null = null;
    let cancelled = false;
    client.subscribeRun(runId).then((c) => {
      if (cancelled) c();
      else cancel = c;
    }).catch(() => { /* no-op */ });
    return () => {
      cancelled = true;
      off();
      cancel?.();
    };
  }, [client, runId]);

  return events;
}
