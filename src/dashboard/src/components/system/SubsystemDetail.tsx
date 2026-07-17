"use client";

import { SystemEventType, type SystemEvent } from "@/types/system-events";
import { EventDrawer, type DrawerEvent, type EventKind } from "@/components/execution/EventDrawer";
import { describeSystemEvent, type SubsystemActivity } from "@/hooks/useSubsystemActivity";
import { cn } from "@/lib/utils";

// p0209b: the detail pane of the rail-driven System master/detail. Renders one
// subsystem's stream in full — section rule + active/idle pill + freshness +
// tail line + the shared EventDrawer (p0205) over the subsystem's typed events.
// An idle subsystem (no event in the freshness window) shows an explicit
// empty-state, never a blank pane.
// p0343d: parity re-dress — the mock's .section-head slim rule (h2 + .cnt.live
// pill + .sh-sub freshness), the newest event as a mono .tail-line, the mock
// .empty state. The standalone pages pass heading="Event stream" (their .m-head
// already names the subsystem); embedded uses (ConfigView) keep the label.

export function SubsystemDetail({
  activity,
  heading,
}: {
  activity: SubsystemActivity;
  heading?: string;
}) {
  const live = activity.live;
  return (
    <section data-testid={`subsystem-detail-${activity.id}`}>
      <div className="section-head">
        <h2 data-testid="subsystem-detail-title">{heading ?? activity.label}</h2>
        <span data-testid="subsystem-detail-pill" className={cn("cnt", live && "live")}>
          {live ? "active" : "idle"}
        </span>
        <span data-testid="subsystem-detail-freshness" className="sh-sub mono">
          {activity.freshness === "—" ? "no recent activity" : activity.freshness}
        </span>
      </div>

      {activity.tail ? (
        <div data-testid="subsystem-detail-tail" className="tail-line">
          <span aria-hidden>↳</span>
          <span>{activity.tail.text}</span>
          <span className="t">{activity.tail.timestamp}</span>
        </div>
      ) : (
        <div style={{ height: 14 }} />
      )}

      {activity.events.length > 0 ? (
        <EventDrawer events={toDrawerEvents(activity.events)} />
      ) : (
        <div data-testid="subsystem-detail-empty" className="empty">
          <div className="ei" aria-hidden>
            ◌
          </div>
          No activity in the freshness window. This subsystem is event-driven and
          idle right now.
        </div>
      )}
    </section>
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
