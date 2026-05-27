"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";
import type { SystemEvent } from "@/types/system-events";

// p0173a: pulls the system event stream via JobsHub.SubscribeSystem.
// Sibling to useRunEvents but system-scoped — no runId, single stream.
// Local cap mirrors the JobsBroadcaster's SystemRecent ring buffer
// capacity (500) so the in-memory window agrees on both sides.

const MAX_SYSTEM_EVENTS = 500;

export function useSystemEvents(): SystemEvent[] {
  const { client } = useJobsHub();
  const [events, setEvents] = useState<SystemEvent[]>([]);

  useEffect(() => {
    setEvents([]);
    const off = client.systemEvents.add((event) => {
      setEvents((prev) => {
        const next = [...prev, event];
        return next.length > MAX_SYSTEM_EVENTS
          ? next.slice(next.length - MAX_SYSTEM_EVENTS)
          : next;
      });
    });
    let cancel: (() => Promise<void>) | null = null;
    let cancelled = false;
    client.subscribeSystem().then((c) => {
      if (cancelled) c();
      else cancel = c;
    }).catch(() => { /* hub may be transitioning; safe to swallow */ });
    return () => {
      cancelled = true;
      off();
      cancel?.();
    };
  }, [client]);

  return events;
}
