"use client";

import { useEffect, useMemo, useState } from "react";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

// p0209b: derives per-subsystem {live, freshness, tail, events} from the
// SystemEvent stream (useSystemEvents). Reuses the freshness-window logic the
// old useSystemExecutionTree applied per row — newest event sets the tail and
// the "how long ago" freshness label; a subsystem with no event inside the
// freshness window is idle. Feeds both the SubsystemDetail pane and (optionally)
// the AppRail dots, so the rail and the detail agree on a single derivation.

export type SubsystemId = "tracker" | "webhooks" | "chat" | "config" | "catalog";

interface SubsystemDef {
  id: SubsystemId;
  label: string;
  kinds: SystemEventType[];
}

// Registry mirrors the AppRail subsystem ids (p0209a) — note `webhooks` (plural)
// matches the rail href /system/webhooks.
export const SUBSYSTEMS: SubsystemDef[] = [
  {
    id: "tracker",
    label: "Tracker · ticket polling",
    kinds: [
      SystemEventType.PollCycleStarted,
      SystemEventType.PollCycleFinished,
      SystemEventType.TicketScanned,
      SystemEventType.TicketSkipped,
      SystemEventType.TicketTriggered,
    ],
  },
  { id: "webhooks", label: "Webhooks", kinds: [SystemEventType.WebhookReceived] },
  { id: "chat", label: "Chat dispatchers", kinds: [SystemEventType.ChatMessageReceived] },
  { id: "config", label: "Config file reads", kinds: [SystemEventType.ConfigFileRead] },
  {
    id: "catalog",
    label: "Skill catalog & vocabulary",
    kinds: [SystemEventType.SkillCatalogLoaded, SystemEventType.ConceptVocabularyLoaded],
  },
];

// A subsystem is "live" when its newest event is within this window. Mirrors the
// old tree's `sinceSec < 120 → ok` freshness threshold.
const LIVE_WINDOW_SECONDS = 120;

export interface SubsystemActivity {
  id: SubsystemId;
  label: string;
  live: boolean;
  /** Human "42s ago" / "now" / "—" when never seen. */
  freshness: string;
  /** Newest typed event for the subsystem, or null when idle. */
  tail: { text: string; timestamp: string } | null;
  /** All events for this subsystem, oldest-first. */
  events: SystemEvent[];
}

export function useSubsystemActivity(events: SystemEvent[]): Record<SubsystemId, SubsystemActivity> {
  // p0264: the "Xs ago" freshness must COUNT without a manual refresh. nowMs used to
  // be captured once per render (only when `events` changed), so the label froze.
  // Tick a second-resolution clock and feed it into the memo so freshness re-derives
  // every second — for both the rail label and the detail pane.
  const nowMs = useNowTick(1000);
  return useMemo(() => buildActivity(events, nowMs), [events, nowMs]);
}

function useNowTick(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

function buildActivity(events: SystemEvent[], nowMs: number): Record<SubsystemId, SubsystemActivity> {
  const out = {} as Record<SubsystemId, SubsystemActivity>;
  for (const def of SUBSYSTEMS) {
    const own = events
      .filter((e) => def.kinds.includes(e.type))
      .sort((a, b) => a.timestamp.localeCompare(b.timestamp));
    if (own.length === 0) {
      out[def.id] = { id: def.id, label: def.label, live: false, freshness: "—", tail: null, events: [] };
      continue;
    }
    const latest = own[own.length - 1];
    const sinceSec = Math.max(0, (nowMs - parseTs(latest.timestamp)) / 1000);
    out[def.id] = {
      id: def.id,
      label: def.label,
      live: sinceSec < LIVE_WINDOW_SECONDS,
      freshness: formatAgo(sinceSec),
      tail: { text: describeSystemEvent(latest), timestamp: shortTime(latest.timestamp) },
      events: own,
    };
  }
  return out;
}

function parseTs(iso: string): number {
  return new Date(iso).getTime();
}

function shortTime(iso: string): string {
  return new Date(iso).toISOString().slice(11, 19);
}

function formatAgo(seconds: number): string {
  if (seconds < 1) return "now";
  if (seconds < 60) return `${Math.round(seconds)}s ago`;
  const m = Math.floor(seconds / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  return `${h}h ago`;
}

export function describeSystemEvent(e: SystemEvent): string {
  switch (e.type) {
    case SystemEventType.PollCycleStarted:
      return `poll start · ${e.tracker} · every ${e.intervalSeconds}s`;
    case SystemEventType.PollCycleFinished:
      return `poll done · ${e.tracker} · ${e.ticketsPolled} polled · ${e.matched} matched · ${e.spawned} spawned · ${e.durationMs}ms`;
    case SystemEventType.TicketScanned:
      return `scan · ${e.tracker}/#${e.ticketId}${e.labels.length > 0 ? " · [" + e.labels.join(", ") + "]" : ""}`;
    case SystemEventType.TicketSkipped:
      return `skip · ${e.tracker}/#${e.ticketId} · ${e.detail}`;
    case SystemEventType.TicketTriggered:
      return `trigger · ${e.tracker}/#${e.ticketId} → ${e.project} / ${e.pipeline} (${e.outcome})`;
    case SystemEventType.WebhookReceived:
      return `${e.eventType} · ${e.path} · ${e.actioned ? "actioned" : "skipped: " + (e.skipReason ?? "")}`;
    case SystemEventType.ChatMessageReceived:
      return `${e.channel} · ${e.messageType} · ${e.actioned ? "actioned" : "skipped: " + (e.skipReason ?? "")}`;
    case SystemEventType.ConfigFileRead:
      return `read ${e.path} (${e.sizeBytes}B)`;
    case SystemEventType.SkillCatalogLoaded:
      return `catalog ${e.catalogVersion} · ${e.skillsLoaded} loaded · ${e.skillsDropped} dropped · ${e.durationMs}ms`;
    case SystemEventType.ConceptVocabularyLoaded:
      return `vocabulary · ${e.conceptCount} concepts · ${e.durationMs}ms`;
    case SystemEventType.ConfigChanged:
      return `config change ${e.epoch} pending · by ${e.actor}`;
    case SystemEventType.ConfigReloaded:
      return `config reload ${e.epoch} applied · ${e.trackerCount} trackers`;
  }
}
