"use client";

import { useEffect, useSyncExternalStore } from "react";
import { useEventStore } from "@/lib/eventStore/EventStoreProvider";
import type { RunEvent } from "@/types/hub-events";

// p0169f / p0218: per-run event log, backed by the shared EventStore. The run's
// backlog persists across remount; the live subscription is ref-counted and
// torn down when the last consumer unmounts or the runId changes. Run events
// stay per-run scoped (the store routes by runId) — only the system firehose
// changed.

const EMPTY: RunEvent[] = [];
const noopSubscribe = (): (() => void) => () => {};
const emptySnapshot = (): RunEvent[] => EMPTY;

export function useRunEvents(runId: string | null): RunEvent[] {
  const store = useEventStore();
  const scope = runId ? store.runScope(runId) : null;

  useEffect(() => scope?.acquire(), [scope]);

  return useSyncExternalStore(
    scope ? scope.subscribeChange : noopSubscribe,
    scope ? scope.getSnapshot : emptySnapshot,
    emptySnapshot,
  );
}
