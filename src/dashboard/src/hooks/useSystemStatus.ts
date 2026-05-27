import { useMemo } from "react";
import {
  SystemEventType,
  type PollCycleFinishedEvent,
  type PollCycleStartedEvent,
  type SystemEvent,
} from "@/types/system-events";

// p0173d: per-tracker connection status derived from the most recent
// PollCycleStarted / Finished pair. If the latest cycle finished within
// 1× intervalSeconds we call it "ok"; up to 3× → "degraded"; beyond → "disconnected".
// "unknown" when we haven't seen any cycle yet.

export type ProviderStatus = "ok" | "degraded" | "disconnected" | "unknown";

export interface SystemStatusEntry {
  source: string;
  tracker: string;
  intervalSeconds: number;
  lastCycleAt: string | null;
  nextEtaAt: string | null;
  status: ProviderStatus;
}

export function useSystemStatus(events: readonly SystemEvent[], now: Date = new Date()): SystemStatusEntry[] {
  return useMemo(() => deriveSystemStatus(events, now.getTime()), [events, now.getTime()]);
}

export function deriveSystemStatus(
  events: readonly SystemEvent[],
  nowMs: number,
): SystemStatusEntry[] {
  const bySource = new Map<string, {
    tracker: string;
    intervalSeconds: number;
    lastCycleFinishedMs: number | null;
    lastCycleStartedMs: number | null;
  }>();

  for (const e of events) {
    if (e.type === SystemEventType.PollCycleStarted) {
      const ev = e as PollCycleStartedEvent;
      const entry = bySource.get(ev.source) ?? {
        tracker: ev.tracker,
        intervalSeconds: ev.intervalSeconds,
        lastCycleFinishedMs: null,
        lastCycleStartedMs: null,
      };
      entry.tracker = ev.tracker;
      entry.intervalSeconds = ev.intervalSeconds;
      entry.lastCycleStartedMs = Date.parse(ev.timestamp);
      bySource.set(ev.source, entry);
    } else if (e.type === SystemEventType.PollCycleFinished) {
      const ev = e as PollCycleFinishedEvent;
      const entry = bySource.get(ev.source) ?? {
        tracker: ev.tracker,
        intervalSeconds: 0,
        lastCycleFinishedMs: null,
        lastCycleStartedMs: null,
      };
      entry.tracker = ev.tracker;
      entry.lastCycleFinishedMs = Date.parse(ev.timestamp);
      bySource.set(ev.source, entry);
    }
  }

  const out: SystemStatusEntry[] = [];
  for (const [source, entry] of bySource) {
    const intervalMs = entry.intervalSeconds * 1000;
    const lastMs = entry.lastCycleFinishedMs ?? entry.lastCycleStartedMs;
    const status: ProviderStatus = lastMs === null
      ? "unknown"
      : nowMs - lastMs <= intervalMs * 1.5 ? "ok"
      : nowMs - lastMs <= intervalMs * 3 ? "degraded"
      : "disconnected";
    out.push({
      source,
      tracker: entry.tracker,
      intervalSeconds: entry.intervalSeconds,
      lastCycleAt: lastMs ? new Date(lastMs).toISOString() : null,
      nextEtaAt: lastMs && intervalMs > 0 ? new Date(lastMs + intervalMs).toISOString() : null,
      status,
    });
  }
  return out.sort((a, b) => a.source.localeCompare(b.source));
}
