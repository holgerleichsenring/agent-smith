import { Suspense } from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0388b: the full-pipeline rail is served from the DB projections, so it paints
// complete even when the client's live event buffer holds NOTHING — the exact
// state a long run reaches once FIFO eviction has taken the structural spine.
// Selecting a step fetches that step's page alone; no view asks for the whole
// trail any more.

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
    },
    connectionState: 1,
    overview: overviewRef.current,
    systemActivity: null,
  }),
}));
// The live buffer is EMPTY — the condition the rail used to collapse under.
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => [] }));
vi.mock("@/hooks/useRunDetailSnapshot", () => ({
  useRunDetailSnapshot: (_runId: string, list: RunSnapshot | null) => list,
}));

const STEPS = [
  {
    stepIndex: 0, stepName: "FetchTicket", displayName: "Fetch ticket", commandName: "FetchTicket",
    status: "success", durationSeconds: 1.5, resultMessage: "T-1 fetched",
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
  },
  {
    stepIndex: 1, stepName: "AnalyzeCode", displayName: "Analyze codebase", commandName: "AnalyzeCode",
    status: "success", durationSeconds: 12, resultMessage: "3 repos mapped",
    llmCalls: 4, costUsd: 0.25, sandboxCommands: 11, subAgents: 2,
  },
  {
    stepIndex: 2, stepName: "SkillRound", displayName: "Implement", commandName: "SkillRound",
    status: "running", durationSeconds: null, resultMessage: null,
    llmCalls: 9, costUsd: 1.5, sandboxCommands: 40, subAgents: 0,
  },
];

const fetchMock = vi.fn();

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "running",
    prUrl: null, summary: null, startedAt: "2026-07-29T09:00:00Z", finishedAt: null, sandboxes: 1,
    stepIndex: 2, stepName: "Implement", totalSteps: 3, lastEventType: null, costUsd: 1.75,
    llmCalls: 13, ticketId: "T-1", ticketTitle: "Fix the login", agentName: "claude",
    cancelRequested: false, ...over,
  };
}

function resolvedParams(id: string): Promise<{ id: string }> {
  const p = Promise.resolve({ id });
  Object.assign(p, { status: "fulfilled", value: { id } });
  return p;
}

function renderRunDetail() {
  overviewRef.current = { active: [snap()], recent: [] };
  return render(
    <EventStoreProvider store={silentEventStore()}>
      <Suspense fallback={null}>
        <RunDetailPage params={resolvedParams("r1")} />
      </Suspense>
    </EventStoreProvider>,
  );
}

function urlsFetched(): string[] {
  return fetchMock.mock.calls.map((c) => String(c[0]));
}

beforeEach(() => {
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url: string) => {
    if (url.includes("/steps/")) {
      return { ok: true, json: async () => ({ events: [], nextSeq: 0, hasMore: false }) };
    }
    if (url.includes("/steps")) return { ok: true, json: async () => ({ steps: STEPS }) };
    if (url.includes("/decisions")) return { ok: true, json: async () => ({ decisions: [] }) };
    return { ok: true, json: async () => ({}) };
  });
  vi.stubGlobal("fetch", fetchMock);
});

describe("Run rail from projections", () => {
  it("NavRail_RendersEveryStep_WhenTheLiveBufferIsEmpty", async () => {
    renderRunDetail();

    const rail = await screen.findByTestId("nav-rail");
    await waitFor(() => expect(rail).toHaveTextContent("Fetch ticket"));
    expect(rail).toHaveTextContent("Analyze codebase");
    expect(rail).toHaveTextContent("Implement");
  });

  it("StepSelection_FetchesOnlyThatStepsPage", async () => {
    renderRunDetail();

    const rail = await screen.findByTestId("nav-rail");
    await waitFor(() => expect(rail).toHaveTextContent("Analyze codebase"));
    const beforeClick = urlsFetched().length;
    fireEvent.click(screen.getByText("Analyze codebase"));

    await waitFor(() =>
      expect(urlsFetched().slice(beforeClick).some((u) => u.includes("/steps/1/events"))).toBe(true));
    // Selecting step 1 fetches step 1's page and nothing else — not the run's
    // trail, and not the other steps' bodies.
    const afterClick = urlsFetched().slice(beforeClick);
    expect(afterClick.filter((u) => /\/steps\/\d+\/events/.test(u))
      .every((u) => u.includes("/steps/1/events"))).toBe(true);
  });

  it("RunDetail_NoRequestReturnsTheWholeTrail", async () => {
    renderRunDetail();

    await waitFor(() => expect(urlsFetched().length).toBeGreaterThan(0));
    // The p0373 whole-trail poll is gone: nothing asks for /trail, and every
    // step-events request is scoped to one step index.
    expect(urlsFetched().some((u) => u.includes("/trail"))).toBe(false);
    expect(urlsFetched().every((u) => !/\/steps\/?$/.test(u) || u.endsWith("/steps"))).toBe(true);
  });
});
