import { describe, it, expect } from "vitest";
import { deriveActivityKpis } from "@/hooks/useActivityKpis";
import {
  SystemEventType,
  TicketSkipReason,
  type SystemEvent,
} from "@/types/system-events";

const NOW = Date.parse("2026-05-27T12:00:00Z");

function scanned(ts: string): SystemEvent {
  return {
    type: SystemEventType.TicketScanned,
    source: "tracker:jira/foo",
    tracker: "foo",
    ticketId: "T-1",
    labels: [],
    timestamp: ts,
  };
}

function triggered(ts: string): SystemEvent {
  return {
    type: SystemEventType.TicketTriggered,
    source: "tracker:jira/foo",
    tracker: "foo",
    ticketId: "T-1",
    project: "foo",
    pipeline: "fix-bug",
    outcome: "Claimed",
    timestamp: ts,
  };
}

function skipped(ts: string): SystemEvent {
  return {
    type: SystemEventType.TicketSkipped,
    source: "tracker:jira/foo",
    tracker: "foo",
    ticketId: "T-1",
    reason: TicketSkipReason.ZeroMatch,
    detail: "no project",
    timestamp: ts,
  };
}

function webhook(ts: string, actioned: boolean): SystemEvent {
  return {
    type: SystemEventType.WebhookReceived,
    source: "webhook:github",
    eventType: "issues",
    path: "/webhooks/github",
    actioned,
    skipReason: actioned ? null : "no-handler-matched",
    timestamp: ts,
  };
}

describe("deriveActivityKpis", () => {
  it("rolling 24h window excludes older events", () => {
    const events: SystemEvent[] = [
      scanned("2026-05-26T11:00:00Z"), // 25h old → excluded
      scanned("2026-05-27T11:00:00Z"), // 1h old → included
    ];
    const kpis = deriveActivityKpis(events, NOW);
    expect(kpis.ticketsScanned).toBe(1);
  });

  it("counts tickets scanned/triggered/skipped separately", () => {
    const events: SystemEvent[] = [
      scanned("2026-05-27T11:00:00Z"),
      scanned("2026-05-27T11:30:00Z"),
      triggered("2026-05-27T11:30:01Z"),
      skipped("2026-05-27T11:00:01Z"),
    ];
    const kpis = deriveActivityKpis(events, NOW);
    expect(kpis.ticketsScanned).toBe(2);
    expect(kpis.ticketsTriggered).toBe(1);
    expect(kpis.ticketsSkipped).toBe(1);
  });

  it("counts webhooks received and actioned-subset", () => {
    const events: SystemEvent[] = [
      webhook("2026-05-27T11:00:00Z", true),
      webhook("2026-05-27T11:00:01Z", false),
      webhook("2026-05-27T11:00:02Z", true),
    ];
    const kpis = deriveActivityKpis(events, NOW);
    expect(kpis.webhooksReceived).toBe(3);
    expect(kpis.webhooksActioned).toBe(2);
  });

  it("empty events returns all zeros", () => {
    const kpis = deriveActivityKpis([], NOW);
    expect(kpis).toEqual({
      ticketsScanned: 0,
      ticketsSkipped: 0,
      ticketsTriggered: 0,
      webhooksReceived: 0,
      webhooksActioned: 0,
      pollCyclesFinished: 0,
    });
  });
});
