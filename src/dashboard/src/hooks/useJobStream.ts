"use client";

import { useEffect, useRef, useState } from "react";
import type { JobStreamEvent, JobStreamStatus } from "@/types/job-stream-events";

interface UseJobStreamOptions {
  fromBeginning?: boolean;
  apiBase?: string;
}

interface UseJobStreamResult {
  events: JobStreamEvent[];
  status: JobStreamStatus;
  reconnecting: boolean;
}

const KNOWN_EVENTS = new Set([
  "progress",
  "tool_call",
  "skill_observation",
  "done",
  "error",
]);

export function useJobStream(jobId: string | null, opts: UseJobStreamOptions = {}): UseJobStreamResult {
  const [events, setEvents] = useState<JobStreamEvent[]>([]);
  const [status, setStatus] = useState<JobStreamStatus>("idle");
  const [reconnecting, setReconnecting] = useState(false);
  const sourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    if (!jobId) return;
    const base = opts.apiBase ?? "";
    const replay = opts.fromBeginning ? "?from_beginning=true" : "";
    const url = `${base}/api/jobs/${encodeURIComponent(jobId)}/stream${replay}`;

    setStatus("connecting");
    const src = new EventSource(url, { withCredentials: false });
    sourceRef.current = src;

    src.onopen = () => {
      setStatus("open");
      setReconnecting(false);
    };

    src.onerror = () => {
      // EventSource auto-reconnects on transient drops; surface the reconnecting flag.
      setReconnecting(true);
      setStatus((s) => (s === "open" ? "reconnecting" : s));
    };

    KNOWN_EVENTS.forEach((name) => {
      src.addEventListener(name, (e) => {
        try {
          const payload = JSON.parse((e as MessageEvent).data);
          setEvents((prev) => [...prev, { type: name, ...payload } as JobStreamEvent]);
          if (name === "done" || name === "error") {
            src.close();
            setStatus("closed");
          }
        } catch {
          // swallow malformed payloads — stream remains open
        }
      });
    });

    return () => {
      src.close();
      sourceRef.current = null;
      setStatus("closed");
    };
  }, [jobId, opts.fromBeginning, opts.apiBase]);

  return { events, status, reconnecting };
}
