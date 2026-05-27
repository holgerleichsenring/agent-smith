import { useMemo } from "react";
import {
  SystemEventType,
  TicketSkipReason,
  type PollCycleFinishedEvent,
  type PollCycleStartedEvent,
  type SystemEvent,
  type TicketScannedEvent,
  type TicketSkippedEvent,
  type TicketTriggeredEvent,
} from "@/types/system-events";

// p0174: aggregates the system event stream into per-poll-cycle rows.
// One row = one (started + finished) pair scoped to a single source;
// tickets scanned/skipped/triggered between those two timestamps from
// the SAME source land inside the cycle. Source-scoped pairing is
// correct under concurrent trackers (timestamp-only pairing would
// mis-attribute).

export interface PullCycleEntry {
  source: string;
  tracker: string;
  startedAt: string;
  finishedAt: string | null;
  durationMs: number | null;
  ticketsPolled: number;
  triggered: number;
  skippedTotal: number;
  skippedByReason: Record<TicketSkipReason, number>;
  scannedTicketIds: readonly string[];
  triggeredEntries: readonly TicketTriggeredEvent[];
  skippedEntries: readonly TicketSkippedEvent[];
}

export function usePullCycleLog(
  events: readonly SystemEvent[],
  limit = 50,
): PullCycleEntry[] {
  return useMemo(() => derivePullCycleLog(events, limit), [events, limit]);
}

export function derivePullCycleLog(
  events: readonly SystemEvent[],
  limit: number,
): PullCycleEntry[] {
  // Index events by source for fast lookup.
  const startsBySource = new Map<string, PollCycleStartedEvent[]>();
  const finishesBySource = new Map<string, PollCycleFinishedEvent[]>();
  const scannedBySource = new Map<string, TicketScannedEvent[]>();
  const skippedBySource = new Map<string, TicketSkippedEvent[]>();
  const triggeredBySource = new Map<string, TicketTriggeredEvent[]>();

  for (const e of events) {
    switch (e.type) {
      case SystemEventType.PollCycleStarted:
        pushTo(startsBySource, e.source, e as PollCycleStartedEvent);
        break;
      case SystemEventType.PollCycleFinished:
        pushTo(finishesBySource, e.source, e as PollCycleFinishedEvent);
        break;
      case SystemEventType.TicketScanned:
        pushTo(scannedBySource, e.source, e as TicketScannedEvent);
        break;
      case SystemEventType.TicketSkipped:
        pushTo(skippedBySource, e.source, e as TicketSkippedEvent);
        break;
      case SystemEventType.TicketTriggered:
        pushTo(triggeredBySource, e.source, e as TicketTriggeredEvent);
        break;
    }
  }

  const cycles: PullCycleEntry[] = [];
  for (const [source, starts] of startsBySource) {
    const finishes = finishesBySource.get(source) ?? [];
    const scanned = (scannedBySource.get(source) ?? []).slice().sort(byTs);
    const skipped = (skippedBySource.get(source) ?? []).slice().sort(byTs);
    const triggered = (triggeredBySource.get(source) ?? []).slice().sort(byTs);
    starts.sort(byTs);
    finishes.sort(byTs);

    for (let i = 0; i < starts.length; i++) {
      const start = starts[i];
      const next = i + 1 < starts.length ? starts[i + 1] : null;
      const startMs = Date.parse(start.timestamp);
      const upperBoundMs = next ? Date.parse(next.timestamp) : Number.POSITIVE_INFINITY;

      // Match the earliest Finished that's >= startMs and before the
      // next Started for the same source.
      const finish = finishes.find((f) => {
        const fMs = Date.parse(f.timestamp);
        return fMs >= startMs && fMs < upperBoundMs;
      }) ?? null;
      const finishMs = finish ? Date.parse(finish.timestamp) : null;

      const cycleEndMs = finishMs ?? Math.min(upperBoundMs, Number.MAX_SAFE_INTEGER);
      const inWindow = <T extends { timestamp: string }>(list: readonly T[]) =>
        list.filter((x) => {
          const ms = Date.parse(x.timestamp);
          return ms >= startMs && ms <= cycleEndMs;
        });

      const cycleScanned = inWindow(scanned);
      const cycleSkipped = inWindow(skipped);
      const cycleTriggered = inWindow(triggered);

      const skippedByReason: Record<TicketSkipReason, number> = {
        [TicketSkipReason.ZeroMatch]: 0,
        [TicketSkipReason.StatusFilter]: 0,
        [TicketSkipReason.ClaimBlocked]: 0,
        [TicketSkipReason.DuplicateInFlight]: 0,
        [TicketSkipReason.NotAnIssue]: 0,
      };
      for (const sk of cycleSkipped) {
        skippedByReason[sk.reason] = (skippedByReason[sk.reason] ?? 0) + 1;
      }

      cycles.push({
        source,
        tracker: start.tracker,
        startedAt: start.timestamp,
        finishedAt: finish?.timestamp ?? null,
        durationMs: finishMs !== null ? finishMs - startMs : null,
        ticketsPolled: finish?.ticketsPolled ?? cycleScanned.length,
        triggered: cycleTriggered.length,
        skippedTotal: cycleSkipped.length,
        skippedByReason,
        scannedTicketIds: cycleScanned.map((s) => s.ticketId),
        triggeredEntries: cycleTriggered,
        skippedEntries: cycleSkipped,
      });
    }
  }

  cycles.sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt));
  return cycles.slice(0, limit);
}

function pushTo<T>(map: Map<string, T[]>, key: string, value: T): void {
  const list = map.get(key);
  if (list) list.push(value);
  else map.set(key, [value]);
}

function byTs<T extends { timestamp: string }>(a: T, b: T): number {
  return Date.parse(a.timestamp) - Date.parse(b.timestamp);
}

export const SKIP_REASON_LABEL: Record<TicketSkipReason, string> = {
  [TicketSkipReason.ZeroMatch]: "no-match",
  [TicketSkipReason.StatusFilter]: "status-filter",
  [TicketSkipReason.ClaimBlocked]: "claim-blocked",
  [TicketSkipReason.DuplicateInFlight]: "duplicate",
  [TicketSkipReason.NotAnIssue]: "not-an-issue",
};
