"use client";

import { useEffect, useMemo, useState } from "react";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

// p0209b: derives per-subsystem {live, freshness, tail, events} from the
// SystemEvent stream (useSystemEvents). Reuses the freshness-window logic the
// old useSystemExecutionTree applied per row — newest event sets the tail and
// the "how long ago" freshness label; a subsystem with no event inside the
// freshness window is idle. Feeds both the SubsystemDetail pane and (optionally)
// the AppRail dots, so the rail and the detail agree on a single derivation.
// 2026-11-22-5485b: the labels, the freshness and the event lines are in the chosen language.

export type SubsystemId = "tracker" | "webhooks" | "chat" | "config" | "catalog";

interface SubsystemDef {
  id: SubsystemId;
  label: TextKey;
  kinds: SystemEventType[];
}

// Registry mirrors the AppRail subsystem ids (p0209a) — note `webhooks` (plural)
// matches the rail href /system/webhooks.
export const SUBSYSTEMS: SubsystemDef[] = [
  {
    id: "tracker",
    label: "system.nav.tracker",
    kinds: [
      SystemEventType.PollCycleStarted,
      SystemEventType.PollCycleFinished,
      SystemEventType.TicketScanned,
      SystemEventType.TicketSkipped,
      SystemEventType.TicketTriggered,
      SystemEventType.TicketLanded,
    ],
  },
  { id: "webhooks", label: "system.nav.webhooks", kinds: [SystemEventType.WebhookReceived] },
  { id: "chat", label: "system.nav.chat", kinds: [SystemEventType.ChatMessageReceived] },
  { id: "config", label: "system.nav.config", kinds: [SystemEventType.ConfigFileRead] },
  {
    id: "catalog",
    label: "system.nav.catalog",
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
  const t = useT();
  return useMemo(() => buildActivity(t, events, nowMs), [t, events, nowMs]);
}

function useNowTick(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

function buildActivity(t: T, events: SystemEvent[], nowMs: number): Record<SubsystemId, SubsystemActivity> {
  const out = {} as Record<SubsystemId, SubsystemActivity>;
  for (const def of SUBSYSTEMS) {
    const own = events
      .filter((e) => def.kinds.includes(e.type))
      .sort((a, b) => a.timestamp.localeCompare(b.timestamp));
    if (own.length === 0) {
      out[def.id] = { id: def.id, label: t(def.label), live: false, freshness: "—", tail: null, events: [] };
      continue;
    }
    const latest = own[own.length - 1];
    const sinceSec = Math.max(0, (nowMs - parseTs(latest.timestamp)) / 1000);
    out[def.id] = {
      id: def.id,
      label: t(def.label),
      live: sinceSec < LIVE_WINDOW_SECONDS,
      freshness: formatAgo(t, sinceSec),
      tail: { text: describeSystemEvent(t, latest), timestamp: shortTime(latest.timestamp) },
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

function formatAgo(t: T, seconds: number): string {
  if (seconds < 1) return t("system.ago.now");
  if (seconds < 60) return t("system.ago.seconds", { count: Math.round(seconds) });
  const m = Math.floor(seconds / 60);
  if (m < 60) return t("system.ago.minutes", { count: m });
  const h = Math.floor(m / 60);
  return t("system.ago.hours", { count: h });
}

/** One event as its stream line; tracker, ids, paths and the server's reasons stay as recorded. */
export function describeSystemEvent(t: T, e: SystemEvent): string {
  const outcome = (actioned: boolean, skipReason: string | null | undefined) =>
    actioned ? t("system.event.actioned") : t("system.event.skipped", { reason: skipReason ?? "" });
  switch (e.type) {
    case SystemEventType.PollCycleStarted:
      return t("system.event.pollStart", { tracker: e.tracker, seconds: e.intervalSeconds });
    case SystemEventType.PollCycleFinished:
      return t("system.event.pollDone", {
        tracker: e.tracker, polled: e.ticketsPolled, matched: e.matched, spawned: e.spawned, ms: e.durationMs,
      });
    case SystemEventType.TicketScanned:
      return t("system.event.scan", { tracker: e.tracker, ticket: e.ticketId, labels: e.labels.length > 0 ? " · [" + e.labels.join(", ") + "]" : "" });
    case SystemEventType.TicketSkipped:
      return t("system.event.skip", { tracker: e.tracker, ticket: e.ticketId, detail: e.detail });
    case SystemEventType.TicketTriggered:
      return t("system.event.trigger", { tracker: e.tracker, ticket: e.ticketId, project: e.project, pipeline: e.pipeline, outcome: e.outcome });
    case SystemEventType.WebhookReceived:
      return `${e.eventType} · ${e.path} · ${outcome(e.actioned, e.skipReason)}`;
    case SystemEventType.ChatMessageReceived:
      return `${e.channel} · ${e.messageType} · ${outcome(e.actioned, e.skipReason)}`;
    case SystemEventType.ConfigFileRead:
      return t("system.event.configRead", { path: e.path, bytes: e.sizeBytes });
    case SystemEventType.SkillCatalogLoaded:
      return t("system.event.catalog", { version: e.catalogVersion, loaded: e.skillsLoaded, dropped: e.skillsDropped, ms: e.durationMs });
    case SystemEventType.ConceptVocabularyLoaded:
      return t("system.event.vocabulary", { count: e.conceptCount, ms: e.durationMs });
    case SystemEventType.ConfigChanged:
      return t("system.event.configChange", { epoch: e.epoch, actor: e.actor });
    case SystemEventType.ConfigReloaded:
      return t("system.event.configReload", { epoch: e.epoch, count: e.trackerCount });
    case SystemEventType.TicketActivityChanged:
      // 2026-10-13-9a59a: a refresh signal the server keeps off the System page.
      return "";
    case SystemEventType.TicketLanded: {
      const ticket = `${e.project}/${/^\d+$/.test(e.ticketId) ? "#" + e.ticketId : e.ticketId}`;
      return e.by ? t("system.event.landedBy", { ticket, kind: e.kind, by: e.by }) : t("system.event.landed", { ticket, kind: e.kind });
    }
  }
}
