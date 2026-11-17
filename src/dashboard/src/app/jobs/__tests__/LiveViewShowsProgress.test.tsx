import { Suspense } from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0423b: THE TWO VIEWS. Progress-watching and failure-diagnosis are different jobs and
// must not share a screen. The live view answers "what is happening" — progress, the
// current spec, the current step — and carries none of the story view's statistics: no
// call-size plot, no per-spec accounting, no call or command tables, and it does not even
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
    specId: "p19106a", status: "success", durationSeconds: 1.5, resultMessage: "T-1 fetched",
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
  },
  {
    stepIndex: 1, stepName: "SkillRound", displayName: "Implement", commandName: "SkillRound",
    specId: "p19106a", status: "running", durationSeconds: null, resultMessage: null,
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
  // 2026-11-17-d6fbb: the graph view is an address now; a case that opened it must not hand it to the next.
  window.history.replaceState(null, "", "/");
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

    // 2026-11-16-6877b: what is happening, in one sentence, and where in the seven sections the run is.
    expect(await screen.findByTestId("run-now-label")).toHaveTextContent("Now — follows the agent");
    expect(screen.getAllByTestId("run-now-line")[0]).toHaveTextContent("Implement");
    await waitFor(() => expect(screen.getByTestId("run-section-build")).toHaveAttribute("data-state", "running"));
    expect(screen.getByTestId("run-section-ready")).toHaveAttribute("data-state", "done");
    expect(screen.getByTestId("run-section-deliver")).toHaveAttribute("data-state", "pending");
    expect(screen.queryByTestId("run-viewer-root")).not.toBeInTheDocument();
    expect(screen.getByTestId("run-open-trace")).toHaveTextContent("Show all 2 steps and the raw trace");

    // None of the story view's statistics are on this screen.
    expect(screen.queryByTestId("call-size-plot")).not.toBeInTheDocument();
    expect(screen.queryByTestId("ticket-statistics")).not.toBeInTheDocument();
    expect(screen.queryByTestId("spec-account")).not.toBeInTheDocument();
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

    expect(await screen.findByTestId("run-open-why")).toHaveAttribute("href", "/jobs/r1/why");
    fireEvent.click(screen.getByTestId("run-show-graph"));
    const link = await screen.findByTestId("side-rail-why");
    expect(link).toHaveTextContent("Why this run did that");
    expect(link).toHaveAttribute("href", "/jobs/r1/why");
    expect(within(screen.getByTestId("run-viewer-root")).getByTestId("side-rail-progress")).toHaveTextContent("of 2 steps");
  });
});
