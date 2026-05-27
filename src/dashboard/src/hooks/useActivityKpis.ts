import { useMemo } from "react";
import {
  SystemEventType,
  type PollCycleFinishedEvent,
  type SystemEvent,
  type TicketScannedEvent,
  type TicketSkippedEvent,
  type TicketTriggeredEvent,
  type WebhookReceivedEvent,
} from "@/types/system-events";

// p0173d: rolling-24h KPI counts derived from the system event stream.
// Pure derivation — no internal state, all numbers computed on demand
// from the source ring buffer.

export interface ActivityKpis {
  ticketsScanned: number;
  ticketsSkipped: number;
  ticketsTriggered: number;
  webhooksReceived: number;
  webhooksActioned: number;
  pollCyclesFinished: number;
}

const ROLLING_WINDOW_MS = 24 * 60 * 60 * 1000;

export function useActivityKpis(events: readonly SystemEvent[], now: Date = new Date()): ActivityKpis {
  return useMemo(() => deriveActivityKpis(events, now.getTime()), [events, now.getTime()]);
}

export function deriveActivityKpis(
  events: readonly SystemEvent[],
  nowMs: number,
): ActivityKpis {
  const cutoff = nowMs - ROLLING_WINDOW_MS;
  let ticketsScanned = 0;
  let ticketsSkipped = 0;
  let ticketsTriggered = 0;
  let webhooksReceived = 0;
  let webhooksActioned = 0;
  let pollCyclesFinished = 0;

  for (const e of events) {
    if (Date.parse(e.timestamp) < cutoff) continue;
    switch (e.type) {
      case SystemEventType.TicketScanned:
        ticketsScanned++;
        void (e as TicketScannedEvent);
        break;
      case SystemEventType.TicketSkipped:
        ticketsSkipped++;
        void (e as TicketSkippedEvent);
        break;
      case SystemEventType.TicketTriggered:
        ticketsTriggered++;
        void (e as TicketTriggeredEvent);
        break;
      case SystemEventType.WebhookReceived: {
        const w = e as WebhookReceivedEvent;
        webhooksReceived++;
        if (w.actioned) webhooksActioned++;
        break;
      }
      case SystemEventType.PollCycleFinished:
        pollCyclesFinished++;
        void (e as PollCycleFinishedEvent);
        break;
    }
  }

  return {
    ticketsScanned,
    ticketsSkipped,
    ticketsTriggered,
    webhooksReceived,
    webhooksActioned,
    pollCyclesFinished,
  };
}
