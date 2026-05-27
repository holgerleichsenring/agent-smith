"use client";

import { useEffect, useState } from "react";
import { useJobsHub } from "./useJobsHub";
import type { RunEvent, SandboxOutputEvent } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";

// p0169f: per-sandbox event ring buffer (last 200 SandboxOutput lines +
// last SandboxCommand + last SandboxResult). Subscribers call expand() to
// open the L3 fanout; the returned cancel collapses it.

const OUTPUT_RING_SIZE = 200;

export interface SandboxFeed {
  command: RunEvent | null;
  result: RunEvent | null;
  outputs: SandboxOutputEvent[];
  expanded: boolean;
}

export function useSandboxEvents(
  runId: string | null,
  repo: string | null,
  expanded: boolean,
): SandboxFeed {
  const { client } = useJobsHub();
  const [feed, setFeed] = useState<SandboxFeed>({
    command: null, result: null, outputs: [], expanded: false,
  });

  useEffect(() => {
    if (!runId || !repo || !expanded) {
      setFeed({ command: null, result: null, outputs: [], expanded: false });
      return;
    }
    const off = client.sandboxEvents.add(({ runId: emittedRunId, repo: emittedRepo, event }) => {
      if (emittedRunId !== runId || emittedRepo !== repo) return;
      setFeed((prev) => applySandboxEvent(prev, event));
    });
    let cancel: (() => Promise<void>) | null = null;
    let cancelled = false;
    client.expandSandbox(runId, repo).then((c) => {
      if (cancelled) c();
      else cancel = c;
    }).catch(() => { /* no-op */ });
    setFeed((prev) => ({ ...prev, expanded: true }));
    return () => {
      cancelled = true;
      off();
      cancel?.();
    };
  }, [client, runId, repo, expanded]);

  return feed;
}

function applySandboxEvent(prev: SandboxFeed, event: RunEvent): SandboxFeed {
  switch (event.type) {
    case EventType.SandboxCommand:
      return { ...prev, command: event, result: null, outputs: [] };
    case EventType.SandboxOutput: {
      const next = [...prev.outputs, event as SandboxOutputEvent];
      return {
        ...prev,
        outputs: next.length > OUTPUT_RING_SIZE
          ? next.slice(next.length - OUTPUT_RING_SIZE)
          : next,
      };
    }
    case EventType.SandboxResult:
      return { ...prev, result: event };
    default:
      return prev;
  }
}
