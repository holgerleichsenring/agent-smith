import { act, render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { InflowPill } from "../InflowPill";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { createFakeSource, silentEventStore, flush } from "@/lib/eventStore/__tests__/fakes";
import { EventStore } from "@/lib/eventStore/eventStore";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0343b: the inflow pill is real data only — last pickup from the NEWEST run
// in the same merged list the home sections render, liveness from the tracker
// subsystem's freshness. No runs → no pill.

let mockOverview: OverviewSnapshot | null = null;
vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    connectionState: 1,
    overview: mockOverview,
    systemActivity: null,
  }),
}));

const renderPill = (store = silentEventStore()) =>
  render(
    <EventStoreProvider store={store}>
      <InflowPill />
    </EventStoreProvider>,
  );

function snap(runId: string, startedAt: string, ticketId: string | null): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "running",
    prUrl: null,
    summary: null,
    startedAt,
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
  };
}

beforeEach(() => {
  mockOverview = null;
});

describe("InflowPill", () => {
  it("InflowPill_NoRuns_RendersNothing", () => {
    mockOverview = { active: [], recent: [], systemActivity: null };
    renderPill();
    expect(screen.queryByTestId("inflow-pill")).not.toBeInTheDocument();
  });

  it("InflowPill_DerivesLastPickupFromNewestRun", () => {
    const now = Date.now();
    mockOverview = {
      active: [snap("old", new Date(now - 60 * 60_000).toISOString(), "1111")],
      recent: [snap("new", new Date(now - 5 * 60_000).toISOString(), "4711")],
      systemActivity: null,
    };
    renderPill();
    // The NEWEST run by startedAt wins, regardless of the active/recent split.
    expect(screen.getByTestId("inflow-pill")).toHaveTextContent("last pickup");
    expect(screen.getByTestId("inflow-pill-pickup")).toHaveTextContent("#4711");
    expect(screen.getByTestId("inflow-pill-pickup")).toHaveTextContent("5m ago");
  });

  it("InflowPill_NewestRunWithoutTicket_OmitsTicketRef", () => {
    mockOverview = {
      active: [snap("r1", new Date().toISOString(), null)],
      recent: [],
      systemActivity: null,
    };
    renderPill();
    const pickup = screen.getByTestId("inflow-pill-pickup");
    expect(pickup).toHaveTextContent("just now");
    expect(pickup.textContent).not.toContain("#");
  });

  it("InflowPill_TrackerIdle_GreyDot_TrackerFresh_LiveDot", async () => {
    mockOverview = {
      active: [snap("r1", new Date().toISOString(), "42")],
      recent: [],
      systemActivity: null,
    };
    const fake = createFakeSource();
    renderPill(new EventStore(fake.source));
    // No tracker event seen → idle.
    expect(screen.getByTestId("inflow-pill")).toHaveAttribute("data-live", "false");
    expect(screen.getByTestId("inflow-pill")).toHaveTextContent("inflow idle");

    const event: SystemEvent = {
      source: "poller",
      type: SystemEventType.PollCycleFinished,
      timestamp: new Date().toISOString(),
      tracker: "azdo",
      ticketsPolled: 1,
      matched: 0,
      spawned: 0,
      statusFiltered: 0,
      zeroMatched: 1,
      durationMs: 80,
    };
    await act(async () => {
      fake.emitSystem(event);
      await flush();
    });
    expect(screen.getByTestId("inflow-pill")).toHaveAttribute("data-live", "true");
    expect(screen.getByTestId("inflow-pill")).toHaveTextContent("inflow live");
  });
});
