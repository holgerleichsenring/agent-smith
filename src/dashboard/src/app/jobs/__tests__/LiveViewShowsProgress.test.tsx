import { Suspense } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0423b: THE TWO VIEWS. Progress-watching and failure-diagnosis are different jobs and
// must not share a screen. The live view answers "what is happening" — progress, the
// current phase, the current step — and carries none of the story view's statistics: no
// call-size plot, no per-phase accounting, no call or command tables, and it does not even
// FETCH them. The story view is one deliberate click away and nowhere else.

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

const STEPS = [
  {
    stepIndex: 0, stepName: "FetchTicket", displayName: "Fetch ticket", commandName: "FetchTicket",
    phaseId: "p19106a", status: "success", durationSeconds: 1.5, resultMessage: "T-1 fetched",
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
  },
  {
    stepIndex: 1, stepName: "SkillRound", displayName: "Implement", commandName: "SkillRound",
    phaseId: "p19106a", status: "running", durationSeconds: null, resultMessage: null,
    llmCalls: 9, costUsd: 1.5, sandboxCommands: 40, subAgents: 0,
  },
];

const fetchMock = vi.fn();

function snap(): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "running",
    prUrl: null, summary: null, startedAt: "2026-08-16T09:00:00Z", finishedAt: null, sandboxes: 1,
    stepIndex: 2, stepName: "Implement", totalSteps: 2, lastEventType: null, costUsd: 1.5,
    llmCalls: 9, ticketId: "T-1", ticketTitle: "Fix the login", agentName: "claude",
    cancelRequested: false,
  };
}

function urlsFetched(): string[] {
  return fetchMock.mock.calls.map((c) => String(c[0]));
}

function renderLiveView() {
  overviewRef.current = { active: [snap()], recent: [], systemActivity: null } as OverviewSnapshot;
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
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url: string) => {
    if (String(url).includes("/steps/")) {
      return { ok: true, json: async () => ({ events: [], newestSeq: 0, hasOlder: false }) };
    }
    if (String(url).includes("/steps")) return { ok: true, json: async () => ({ steps: STEPS }) };
    if (String(url).includes("/decisions")) return { ok: true, json: async () => ({ decisions: [] }) };
    return { ok: true, json: async () => ({}) };
  });
  vi.stubGlobal("fetch", fetchMock);
});

describe("The live view", () => {
  it("LiveView_ShowsProgress_AndNoStatistics", async () => {
    renderLiveView();

    // What is happening: how far the run has got, and what it is on right now.
    const progress = await screen.findByTestId("side-rail-progress");
    expect(progress).toHaveTextContent("2");
    expect(progress).toHaveTextContent("of 2 steps");
    expect(screen.getByTestId("run-viewer-root")).toHaveTextContent("on Implement");

    // None of the story view's statistics are on this screen.
    expect(screen.queryByTestId("call-size-plot")).not.toBeInTheDocument();
    expect(screen.queryByTestId("ticket-statistics")).not.toBeInTheDocument();
    expect(screen.queryByTestId("phase-account")).not.toBeInTheDocument();
    expect(screen.queryByTestId("call-table")).not.toBeInTheDocument();
    expect(screen.queryByTestId("command-table")).not.toBeInTheDocument();
  });

  it("LiveView_NeverFetchesTheDiagnosisPayload", async () => {
    renderLiveView();

    await waitFor(() => expect(urlsFetched().length).toBeGreaterThan(0));
    // Recording everything is cheap; SHOWING it is the expensive decision. The live
    // surface does not even ask for the fold or the recorded conversation.
    expect(urlsFetched().some((u) => u.includes("/statistics"))).toBe(false);
    expect(urlsFetched().some((u) => u.includes("/trace"))).toBe(false);
  });

  it("LiveView_OffersTheStoryView_AsADeliberateStep", async () => {
    renderLiveView();

    const link = await screen.findByTestId("side-rail-why");
    expect(link).toHaveTextContent("Why this run did that");
    expect(link).toHaveAttribute("href", "/jobs/r1/why");
  });
});
