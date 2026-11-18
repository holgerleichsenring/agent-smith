import { Suspense } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// 2026-09-22-7c41c: the run's own page says "needs you" in FIVE places, each derived from the
// status alone — the side rail's state label, the header's status spill, the phrase beside it,
// the full-width banner (which renders whether or not a question is attached) and the story
// spine. Fixing some and leaving others would put two contradictory claims about one run on
// one screen. A parked run whose relaunch is under way raises none of them; a parked run with
// no place in the queue raises all of them exactly as before.

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => "/",
}));

const overviewRef: { current: OverviewSnapshot | null } = { current: null };
vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {
      getResultMarkdown: () => Promise.resolve(null),
      getPlanMarkdown: () => Promise.resolve(null),
      getAnalyzeMarkdown: () => Promise.resolve(null),
      getSpecMarkdown: () => Promise.resolve(null),
    },
    connectionState: 1,
    overview: overviewRef.current,
    systemActivity: null,
  }),
}));
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => [] }));
vi.mock("@/hooks/useRunDetailSnapshot", () => ({
  useRunDetailSnapshot: (_runId: string, list: RunSnapshot | null) => list,
}));

const BEATS = {
  ticket: "done",
  plan: "done",
  building: "active",
  verify: "pending",
  outcome: "pending",
} as const;

function parked(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"],
    status: "waiting_for_input", prUrl: null,
    summary: "Waiting for an operator answer — checkpointed; compute released.",
    startedAt: "2026-09-22T09:00:00Z", finishedAt: null, sandboxes: 1,
    stepIndex: 2, stepName: "Implement", totalSteps: 4, lastEventType: null, costUsd: 1.5,
    llmCalls: 9, ticketId: "T-1", ticketTitle: "Fix the login", agentName: "claude",
    cancelRequested: false, beats: BEATS, ...over,
  };
}

function renderPage(snapshot: RunSnapshot) {
  overviewRef.current = { active: [snapshot], recent: [], systemActivity: null } as OverviewSnapshot;
  const p = Promise.resolve({ id: "r1" });
  Object.assign(p, { status: "fulfilled", value: { id: "r1" } });
  return render(
    <EventStoreProvider store={silentEventStore()}>
      <Suspense fallback={null}>
        <RunDetailPage params={p} />
      </Suspense>
    </EventStoreProvider>,
  );
}

beforeEach(() => {
  // 2026-11-17-d6fbb: the graph view is an address now; a case that opened it must not hand it to the next.
  window.history.replaceState(null, "", "/");
  vi.stubGlobal("fetch", vi.fn(async (url: string) => {
    if (String(url).includes("/steps/")) {
      return { ok: true, json: async () => ({ events: [], newestSeq: 0, hasOlder: false }) };
    }
    if (String(url).includes("/steps")) return { ok: true, json: async () => ({ steps: [] }) };
    if (String(url).includes("/decisions")) return { ok: true, json: async () => ({ decisions: [] }) };
    return { ok: true, json: async () => ({}) };
  }));
});

describe("The run page of a parked run", () => {
  it("RunPage_AWaitingRunWithAQueuePlace_SaysNeedsYouInNoneOfTheFivePlaces", async () => {
    renderPage(parked({ queuePosition: 3 }));

    // 1. the status sentence — 2026-11-16-6877b: the default view's one line, no question card.
    expect(await screen.findByTestId("run-now-line")).toHaveTextContent("Answered, place 3 in line. Nothing for you to do.");
    expect(screen.getByTestId("run-meta-state")).toHaveTextContent("resuming, place 3");
    expect(screen.getByTestId("run-meta-state")).toHaveAttribute("data-tone", "run");
    expect(screen.queryByTestId("run-question")).not.toBeInTheDocument();
    // 2026-11-17-6d9ed: the graph view's wrapper, banner, side rail and story spine are gone; the page says it once.
    expect(screen.getByTestId("run-log")).not.toHaveTextContent("Needs you");
    expect(screen.getByTestId("run-log")).not.toHaveTextContent("needs you");
  });

  it("RunPage_AWaitingRunWithNoQueuePlace_StillSaysNeedsYou", async () => {
    renderPage(parked());

    expect(await screen.findByTestId("run-now-label")).toHaveTextContent("Waiting for your answer");
    expect(screen.getByTestId("run-meta-state")).toHaveTextContent("needs you");
    expect(screen.getByTestId("run-meta-state")).toHaveAttribute("data-tone", "amber");
  });
});

// 2026-11-17-d6fbb: the default log is complete without the graph. 2026-11-17-6d9ed: and the graph is gone.
describe("The run page without the graph", () => {
  it("RunPage_DefaultLog_CarriesTheTicketPlanAndOutcomeSections_Closed", async () => {
    renderPage(parked());
    for (const id of ["ticket", "plan", "deliver"]) {
      const section = await screen.findByTestId(`run-section-${id}`);
      expect(section.querySelector("button")).toHaveAttribute("aria-expanded", "false");
    }
    fireEvent.click(screen.getByTestId("run-section-ticket").querySelector("button")!);
    expect(screen.getByTestId("ticket-body")).toBeInTheDocument();
    expect(screen.getByTestId("run-details")).toHaveTextContent("run r1");
    expect(screen.getByTestId("run-now")).toHaveTextContent("Reserved compute is held");
    expect(screen.queryByTestId("run-viewer-root")).not.toBeInTheDocument();
  });

  it("RunPage_AnOldGraphAddress_ShowsTheLog", async () => {
    window.history.replaceState(null, "", "/jobs/r1?view=graph");
    renderPage(parked());
    expect(await screen.findByTestId("run-log")).toBeInTheDocument();
    expect(screen.queryByTestId("run-viewer-root")).toBeNull();
    expect(screen.queryByTestId("run-show-graph")).toBeNull();
  });
});
