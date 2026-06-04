"use client";

import { useMemo, useSyncExternalStore } from "react";
import { useEventStore } from "@/lib/eventStore/EventStoreProvider";
import { SUBSYSTEMS, type SubsystemId } from "./useSubsystemActivity";
import type { SystemEvent } from "@/types/system-events";

// p0218: replaces useSystemEvents. Reads the shared, persistent system backlog
// from the EventStore — no per-component subscribeSystem, no setEvents([]) on
// mount. The full backlog feeds the rail (every subsystem's liveness); a single
// subsystem's scoped slice feeds its detail view.

const EMPTY: SystemEvent[] = [];

export function useSystemBacklog(): SystemEvent[] {
  const scope = useEventStore().systemScope();
  return useSyncExternalStore(scope.subscribeChange, scope.getSnapshot, () => EMPTY);
}

export function useSubsystemEvents(id: SubsystemId): SystemEvent[] {
  const all = useSystemBacklog();
  const kinds = useMemo(() => new Set(SUBSYSTEMS.find((s) => s.id === id)!.kinds), [id]);
  return useMemo(() => all.filter((event) => kinds.has(event.type)), [all, kinds]);
}
