"use client";

import { useMemo } from "react";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { EventDrawer, type DrawerEvent, type EventKind } from "@/components/execution/EventDrawer";

// p0183: same shape as useRunExecutionTree, but the rows are subsystems
// (tracker / webhooks / chat / config / skill-catalog) and the time axis
// is "the last hour" — every subsystem's bar shows when it last ran
// relative to "now". Static subsystem registry, dynamic content per
// subsystem from the SystemEvent stream.

interface SystemBucket {
  id: string;
  label: string;
  kinds: SystemEventType[];
  events: SystemEvent[];
}

const REGISTRY: Array<Pick<SystemBucket, "id" | "label" | "kinds">> = [
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
  {
    id: "webhook",
    label: "Webhooks",
    kinds: [SystemEventType.WebhookReceived],
  },
  {
    id: "chat",
    label: "Chat dispatchers",
    kinds: [SystemEventType.ChatMessageReceived],
  },
  {
    id: "config",
    label: "Config file reads",
    kinds: [SystemEventType.ConfigFileRead],
  },
  {
    id: "catalog",
    label: "Skill catalog & vocabulary",
    kinds: [SystemEventType.SkillCatalogLoaded, SystemEventType.ConceptVocabularyLoaded],
  },
];

export interface SystemExecutionTree {
  nodes: ExecutionNodeProps[];
  windowSeconds: number;
}

export function useSystemExecutionTree(events: SystemEvent[]): SystemExecutionTree {
  return useMemo(() => buildSystemTree(events), [events]);
}

function buildSystemTree(events: SystemEvent[]): SystemExecutionTree {
  const buckets = REGISTRY.map<SystemBucket>((r) => ({ ...r, events: [] }));
  for (const e of events) {
    const bucket = buckets.find((b) => b.kinds.includes(e.type));
    if (bucket) bucket.events.push(e);
  }

  // Window = max(60s, time-since-oldest-event). Bars are positioned right-
  // anchored relative to "now" — newer events sit further right.
  const nowMs = Date.now();
  const oldestMs = events.length === 0
    ? nowMs
    : Math.min(...events.map((e) => parseTs(e.timestamp)));
  const windowMs = Math.max(60_000, nowMs - oldestMs);
  const windowSeconds = windowMs / 1000;

  const nodes: ExecutionNodeProps[] = buckets.map((b) => bucketToNode(b, nowMs, windowSeconds));
  return { nodes, windowSeconds };
}

function bucketToNode(b: SystemBucket, nowMs: number, windowSeconds: number): ExecutionNodeProps {
  if (b.events.length === 0) {
    return {
      id: `sys-${b.id}`,
      label: b.label,
      status: "wait",
      depth: 0,
      startSeconds: 0,
      durationSeconds: 0,
      totalSeconds: windowSeconds,
      durationLabel: "—",
      body: <div className="py-2 text-sm text-stone-400">No activity in window.</div>,
    };
  }
  const sorted = [...b.events].sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  const latest = sorted[sorted.length - 1];
  const latestMs = parseTs(latest.timestamp);
  const sinceSec = Math.max(0, (nowMs - latestMs) / 1000);
  // p0190: bar grows from the left in proportion to how FRESH the most
  // recent activity is. A full-width bar means "active right now"; a
  // short bar means "stale". The previous shape anchored a tiny segment
  // at the right edge — semantically correct ("time since last event")
  // but it read as "almost nothing is happening" precisely when an
  // operator most wanted to see "things are running." Invert: fill =
  // (windowSeconds - sinceSec) / windowSeconds.
  const startSec = 0;
  const durationSec = Math.max(0.5, windowSeconds - sinceSec);
  const status: NodeStatus = sinceSec < 120 ? "ok" : sinceSec < 600 ? "run" : "wait";

  return {
    id: `sys-${b.id}`,
    label: b.label,
    status,
    depth: 0,
    startSeconds: startSec,
    durationSeconds: durationSec,
    totalSeconds: windowSeconds,
    durationLabel: formatAgo(sinceSec),
    tail: { text: describeSystemEvent(latest), timestamp: shortTime(latest.timestamp) },
    body: <EventDrawer events={toDrawerEvents(sorted)} />,
  };
}

function toDrawerEvents(es: SystemEvent[]): DrawerEvent[] {
  return es.map((e, idx) => ({
    id: `${e.type}-${e.timestamp}-${idx}`,
    timestamp: shortTime(e.timestamp),
    kind: kindOfSystem(e.type),
    body: <span>{describeSystemEvent(e)}</span>,
    searchText: describeSystemEvent(e),
  }));
}

function kindOfSystem(type: SystemEventType): EventKind {
  switch (type) {
    case SystemEventType.WebhookReceived:
    case SystemEventType.ChatMessageReceived:
      return "tool";
    case SystemEventType.SkillCatalogLoaded:
    case SystemEventType.ConceptVocabularyLoaded:
    case SystemEventType.ConfigFileRead:
      return "file";
    case SystemEventType.TicketTriggered:
      return "dec";
    case SystemEventType.TicketScanned:
    case SystemEventType.TicketSkipped:
    case SystemEventType.PollCycleStarted:
    case SystemEventType.PollCycleFinished:
    default:
      return "obs";
  }
}

function describeSystemEvent(e: SystemEvent): string {
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
