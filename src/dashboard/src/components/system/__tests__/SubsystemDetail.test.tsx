import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { SubsystemDetail } from "../SubsystemDetail";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

function pollFinished(timestamp: string): SystemEvent {
  return {
    source: "tracker",
    type: SystemEventType.PollCycleFinished,
    timestamp,
    tracker: "sample",
    ticketsPolled: 49,
    matched: 0,
    spawned: 0,
    statusFiltered: 0,
    zeroMatched: 49,
    durationMs: 269,
  };
}

const active: SubsystemActivity = {
  id: "tracker",
  label: "Tracker · ticket polling",
  live: true,
  freshness: "42s ago",
  tail: { text: "poll done · sample · 49 polled", timestamp: "19:09:30" },
  events: [pollFinished("2026-06-03T19:09:30.000Z")],
};

const idle: SubsystemActivity = {
  id: "webhooks",
  label: "Webhooks",
  live: false,
  freshness: "—",
  tail: null,
  events: [],
};

describe("SubsystemDetail", () => {
  it("SubsystemDetail_ActiveSubsystem_RendersPillTailAndDrawer", () => {
    render(<SubsystemDetail activity={active} />);

    expect(screen.getByTestId("subsystem-detail-title")).toHaveTextContent("Tracker");
    expect(screen.getByTestId("subsystem-detail-pill")).toHaveTextContent("active");
    expect(screen.getByTestId("subsystem-detail-freshness")).toHaveTextContent("42s ago");
    expect(screen.getByTestId("subsystem-detail-tail")).toHaveTextContent("poll done");
    // the shared EventDrawer renders the typed event stream.
    expect(screen.getByTestId("event-drawer")).toBeInTheDocument();
    expect(screen.queryByTestId("subsystem-detail-empty")).not.toBeInTheDocument();
  });

  it("SubsystemDetail_IdleSubsystem_RendersEmptyState", () => {
    render(<SubsystemDetail activity={idle} />);

    expect(screen.getByTestId("subsystem-detail-pill")).toHaveTextContent("idle");
    expect(screen.getByTestId("subsystem-detail-freshness")).toHaveTextContent("no recent activity");
    // explicit empty-state, not a blank pane / drawer.
    expect(screen.getByTestId("subsystem-detail-empty")).toBeInTheDocument();
    expect(screen.queryByTestId("event-drawer")).not.toBeInTheDocument();
  });
});
