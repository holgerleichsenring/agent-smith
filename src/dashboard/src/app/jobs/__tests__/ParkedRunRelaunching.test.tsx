import { Suspense } from "react";
import { render, screen } from "@testing-library/react";
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

    // 1. the header's status spill — and with it the wrapper class that themes the page.
    expect(await screen.findByTestId("run-status-spill")).toHaveTextContent("Resuming");
    expect(screen.getByTestId("run-viewer-root").className).toContain("is-prov");
    expect(screen.getByTestId("run-viewer-root").className).not.toContain("is-blocked");
    // 2. the phrase beside it — a different file from the header.
    expect(screen.getByTestId("run-viewer-root")).toHaveTextContent("resuming — place 3 in line");
    // 3. the full-width banner, which renders with or without a question attached.
    expect(screen.queryByTestId("run-banner")).not.toBeInTheDocument();
    // 4. the side rail's state label.
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("Resuming · place 3");
    // 5. the story spine.
    expect(screen.getByTestId("beat-section-badge")).toHaveTextContent("resuming");

    expect(screen.getByTestId("run-viewer-root")).not.toHaveTextContent("Needs you");
  });

  it("RunPage_AWaitingRunWithNoQueuePlace_StillSaysNeedsYou", async () => {
    renderPage(parked());

    expect(await screen.findByTestId("run-status-spill")).toHaveTextContent("Needs you");
    expect(screen.getByTestId("run-viewer-root").className).toContain("is-blocked");
    expect(screen.getByTestId("run-viewer-root")).toHaveTextContent("paused on an open question");
    expect(screen.getByTestId("run-banner")).toBeInTheDocument();
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("Needs you");
    expect(screen.getByTestId("beat-section-badge")).toHaveTextContent("paused · needs you");
  });
});
