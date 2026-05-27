import { useMemo } from "react";
import {
  SystemEventType,
  type SystemEvent,
  type WebhookReceivedEvent,
} from "@/types/system-events";

// p0174: one row per webhook HTTP delivery. Most-recent-first. Pure
// projection from the system event stream — no aggregation since each
// delivery is its own logical unit.

export interface WebhookLogEntry {
  source: string;
  eventType: string;
  path: string;
  actioned: boolean;
  skipReason: string | null;
  timestamp: string;
}

export function useWebhookLog(
  events: readonly SystemEvent[],
  limit = 50,
): WebhookLogEntry[] {
  return useMemo(() => deriveWebhookLog(events, limit), [events, limit]);
}

export function deriveWebhookLog(
  events: readonly SystemEvent[],
  limit: number,
): WebhookLogEntry[] {
  const entries: WebhookLogEntry[] = [];
  for (const e of events) {
    if (e.type !== SystemEventType.WebhookReceived) continue;
    const ev = e as WebhookReceivedEvent;
    entries.push({
      source: ev.source,
      eventType: ev.eventType,
      path: ev.path,
      actioned: ev.actioned,
      skipReason: ev.skipReason,
      timestamp: ev.timestamp,
    });
  }
  entries.sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp));
  return entries.slice(0, limit);
}
