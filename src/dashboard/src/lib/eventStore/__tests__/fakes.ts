import type { HubEventSource } from "../eventStore";
import { EventStore } from "../eventStore";
import type { SystemEvent } from "@/types/system-events";
import type { RunEvent } from "@/types/hub-events";

// p0218 test support: a controllable HubEventSource. Lets a test drive the
// system/run streams and inspect subscribe/cancel/listener bookkeeping so the
// no-leak + scope-isolation guarantees are assertable without a real hub.
export function createFakeSource() {
  const systemListeners = new Set<(event: SystemEvent) => void>();
  const runListeners = new Set<(entry: { runId: string; event: RunEvent }) => void>();
  const counts = { systemSubs: 0, runSubs: 0, systemCancels: 0, runCancels: 0 };

  const source: HubEventSource = {
    systemEvents: {
      add(listener) {
        systemListeners.add(listener);
        return () => systemListeners.delete(listener);
      },
    },
    runEvents: {
      add(listener) {
        runListeners.add(listener);
        return () => runListeners.delete(listener);
      },
    },
    subscribeSystem() {
      counts.systemSubs++;
      return Promise.resolve(async () => {
        counts.systemCancels++;
      });
    },
    subscribeRun() {
      counts.runSubs++;
      return Promise.resolve(async () => {
        counts.runCancels++;
      });
    },
  };

  return {
    source,
    emitSystem: (event: SystemEvent) => systemListeners.forEach((l) => l(event)),
    emitRun: (runId: string, event: RunEvent) => runListeners.forEach((l) => l({ runId, event })),
    counts: () => ({ ...counts }),
    systemListenerCount: () => systemListeners.size,
    runListenerCount: () => runListeners.size,
  };
}

/** A store wired to a silent source — for component tests that need a provider
    but do not drive events. */
export function silentEventStore(): EventStore {
  return new EventStore(createFakeSource().source);
}

/** Flush the microtask + macrotask queues so async subscribe/cancel settle. */
export const flush = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));
