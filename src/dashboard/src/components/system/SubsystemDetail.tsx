"use client";

import { SystemEventType, type SystemEvent } from "@/types/system-events";
import { EventDrawer, type DrawerEvent, type EventKind } from "@/components/execution/EventDrawer";
import { describeSystemEvent, type SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0209b: the detail pane of the rail-driven System master/detail. Renders one
// subsystem in full — "System ›" crumb + title + active/idle pill + freshness +
// tail line + the shared EventDrawer (p0205) over the subsystem's typed events.
// An idle subsystem (no event in the freshness window) shows an explicit
// empty-state, never a blank pane. Mirrors the subsystem-detail block in
// p0209-redesign-system.html.

export function SubsystemDetail({ activity }: { activity: SubsystemActivity }) {
  const live = activity.live;
  return (
    <div data-testid={`subsystem-detail-${activity.id}`} className="content-shell">
      <div className="breadcrumb">System ›</div>
      <div className="mt-1 flex items-center gap-3 dsh-h2 font-semibold tracking-tight">
        <span data-testid="subsystem-detail-title">{activity.label}</span>
        <span
          data-testid="subsystem-detail-pill"
          className={`rounded-full px-2.5 py-0.5 dsh-mono font-semibold ${
            live ? "bg-emerald-50 text-emerald-700" : "bg-stone-100 text-stone-500"
          }`}
        >
          {live ? "active" : "idle"}
        </span>
        <span data-testid="subsystem-detail-freshness" className="ml-auto font-mono dsh-body text-stone-500">
          {activity.freshness === "—" ? "no recent activity" : activity.freshness}
        </span>
      </div>

      {activity.tail ? (
        <div
          data-testid="subsystem-detail-tail"
          className="my-4 flex items-center gap-2.5 rounded-lg border border-stone-200 bg-[#faf8f4] px-3.5 py-2.5 font-mono dsh-body text-stone-600"
        >
          <span className="text-stone-400" aria-hidden>↳</span>
          <span>{activity.tail.text}</span>
          <span className="ml-auto text-stone-400">{activity.tail.timestamp}</span>
        </div>
      ) : (
        <div className="h-4" />
      )}

      <div className="border-t border-stone-200 pt-4">
        {activity.events.length > 0 ? (
          <EventDrawer events={toDrawerEvents(activity.events)} />
        ) : (
          <div data-testid="subsystem-detail-empty" className="py-8 text-center text-sm text-stone-500">
            No activity in the freshness window. This subsystem is event-driven and
            idle right now.
          </div>
        )}
      </div>
    </div>
  );
}

function toDrawerEvents(events: SystemEvent[]): DrawerEvent[] {
  return events.map((e, idx) => {
    const text = describeSystemEvent(e);
    return {
      id: `${e.type}-${e.timestamp}-${idx}`,
      timestamp: shortTime(e.timestamp),
      kind: kindOfSystem(e.type),
      body: <span>{text}</span>,
      searchText: text,
    };
  });
}

// Map subsystem event types onto the shared EventDrawer kinds (poll/obs/file/
// catalog grouping from the mockup → the drawer's obs/file/tool/dec vocabulary).
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

function shortTime(iso: string): string {
  return new Date(iso).toISOString().slice(11, 19);
}
