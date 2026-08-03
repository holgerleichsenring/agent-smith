import { Suspense } from "react";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0388d: the step-detail pane shows a step's NEWEST events, so on a step that
// outgrew one page it is showing a slice — and it says so, with the walk back
// into history as an explicit control rather than a page that looks complete.

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
    stepIndex: 1, stepName: "SkillRound", displayName: "Implement", commandName: "SkillRound",
    status: "success", durationSeconds: 6200, resultMessage: "3 repos changed",
    llmCalls: 900, costUsd: 41, sandboxCommands: 5400, subAgents: 6,
  },
];

const fetchMock = vi.fn();

// The run is FINISHED — the state in which an operator opens a 103-minute step
// to find out what happened at its end.
function snap(): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "success",
    prUrl: null, summary: null, startedAt: "2026-07-29T09:00:00Z",
    finishedAt: "2026-07-29T10:43:00Z", sandboxes: 1,
    stepIndex: 1, stepName: "Implement", totalSteps: 2, lastEventType: null, costUsd: 41,
    llmCalls: 900, ticketId: "T-1", ticketTitle: "Fix the login", agentName: "claude",
    cancelRequested: false,
  };
}

function resolvedParams(id: string): Promise<{ id: string }> {
  const p = Promise.resolve({ id });
  Object.assign(p, { status: "fulfilled", value: { id } });
  return p;
}

function renderRunDetail() {
  overviewRef.current = { active: [], recent: [snap()], systemActivity: null };
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
  // Braced on purpose: a concise arrow RETURNS the mock, and vitest treats a
  // value returned from a hook as its teardown callback.
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url: string) => {
    if (url.includes("/events")) {
      // A step far larger than one page: whichever end is read, older rows
      // remain below it.
      return {
        ok: true,
        json: async () => ({ events: [], oldestSeq: 4900, newestSeq: 5000, hasOlder: true }),
      };
    }
    if (url.includes("/steps")) return { ok: true, json: async () => ({ steps: STEPS }) };
    if (url.includes("/decisions")) return { ok: true, json: async () => ({ decisions: [] }) };
    return { ok: true, json: async () => ({}) };
  });
  vi.stubGlobal("fetch", fetchMock);
});

describe("Step detail follows the run", () => {
  it("StepDetail_MoreRowsThanShown_SaysOlderRowsExist", async () => {
    renderRunDetail();

    const rail = await screen.findByTestId("nav-rail");
    await waitFor(() => expect(rail).toHaveTextContent("Implement"));
    fireEvent.click(within(rail).getByText("Implement"));

    const notice = await screen.findByTestId("step-events-older-notice");
    expect(notice).toHaveTextContent("older ones exist");

    // History is reachable, and the walk is anchored on the page's own oldest
    // row rather than starting over from the top of the step.
    fireEvent.click(screen.getByTestId("step-events-load-older"));
    await waitFor(() =>
      expect(urlsFetched().some((u) => u.includes("beforeSeq=4900"))).toBe(true));
  });

  it("StepDetail_OpeningAStep_AsksForItsNewestPage", async () => {
    renderRunDetail();

    const rail = await screen.findByTestId("nav-rail");
    await waitFor(() => expect(rail).toHaveTextContent("Implement"));
    fireEvent.click(within(rail).getByText("Implement"));

    await waitFor(() =>
      expect(urlsFetched().some((u) => /\/steps\/1\/events$/.test(u))).toBe(true));
    // No cursor at all: sinceSeq=0 was the old opening request, and reading
    // forward from nothing is what returned a long step's FIRST page.
    expect(urlsFetched().some((u) => u.includes("sinceSeq"))).toBe(false);
  });

  it("StepDetail_RunFinished_StopsPolling", async () => {
    renderRunDetail();

    const rail = await screen.findByTestId("nav-rail");
    await waitFor(() => expect(rail).toHaveTextContent("Implement"));
    fireEvent.click(within(rail).getByText("Implement"));
    await screen.findByTestId("step-events-older-notice");

    // A finished run costs nothing: no request repeats over the poll interval.
    const settled = urlsFetched().length;
    await new Promise((resolve) => setTimeout(resolve, 1300));
    expect(urlsFetched().length).toBe(settled);
  });
});
