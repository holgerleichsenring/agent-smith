import { Suspense } from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import RunDetailPage from "@/app/jobs/[id]/page";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import { EventType, type MasterPassEndedEvent, type OverviewSnapshot, type RunEvent, type RunSnapshot } from "@/types/hub-events";

// 2026-11-24-0fbfa: the recorded shape of a live code run whose one spec got a structure correction — step 22 ran the
// master (pass 1, ended nothing-left), step 24 committed and queued one correction pass, step 25 runs the master again.
// The page reads the correction as the spec going on: pass 1's step and the checklist stay done, pass 2 says what it is
// and why, and the Now block follows it.

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

const SPEC = "2026-11-24-abcd";
const PASS_ONE_END: MasterPassEndedEvent = {
  runId: "r1", type: EventType.MasterPassEnded, timestamp: "2026-11-24T10:14:00Z", originStepIndex: 22, specId: SPEC,
  ordinal: 1, passInStep: 0, openedBecause: "first", endedBecause: "nothing-left", verdictEmitted: false,
  gateLine: "", actionablePending: 0, unbackedDone: 0,
};
const eventsRef: { current: RunEvent[] } = { current: [PASS_ONE_END] };
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => eventsRef.current }));
vi.mock("@/hooks/useRunDetailSnapshot", () => ({
  useRunDetailSnapshot: (_runId: string, list: RunSnapshot | null) => list,
}));

const CORRECTION = "1 unit(s) over their Limits; one correction pass follows\n- src/A.cs A.Long(): function_lines 70 > 55";

function row(stepIndex: number, commandName: string, displayName: string, status: string | null, over: Record<string, unknown> = {}) {
  return {
    stepIndex, stepName: commandName, displayName, commandName, specId: SPEC, stepClass: "milestone", status,
    durationSeconds: status === "success" ? 60 : null, resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0,
    subAgents: 0, planned: status === null, ...over,
  };
}

const STEPS = [
  row(22, "AgenticMaster", "Run master skill", "success", { llmCalls: 58, costUsd: 1.08, durationSeconds: 840, resultMessage: "Master finished" }),
  row(24, "CommitSpecWork", "Commit the spec's work", "success", { resultMessage: CORRECTION }),
  row(25, "AgenticMaster", "Run master skill", "running", { llmCalls: 3, costUsd: 0.05 }),
  row(27, "CommitSpecWork", "Commit the spec's work", null),
  row(28, "VerifySpec", "Build and test", null),
];

function snap(): RunSnapshot {
  return {
    runId: "r1", pipeline: "code", trigger: "ticket", repos: ["server"], status: "running", prUrl: null, summary: null,
    startedAt: "2026-11-24T09:40:00Z", finishedAt: null, sandboxes: 1, stepIndex: 25, stepName: "Run master skill",
    totalSteps: 33, lastEventType: null, costUsd: 1.13, llmCalls: 61, ticketId: "T-1", ticketTitle: "Fix the login",
    agentName: "claude", cancelRequested: false,
    progressLedger: [
      { id: "s1", activity: "Add the check", status: "done", target: null },
      { id: "s2", activity: "Cover it with a test", status: "done", target: null },
    ],
  };
}

const fetchMock = vi.fn();

function renderPage() {
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
  window.history.replaceState(null, "", "/");
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url: string) => {
    if (String(url).includes("/steps/")) return { ok: true, json: async () => ({ events: [], newestSeq: 0, hasOlder: false }) };
    if (String(url).includes("/steps")) return { ok: true, json: async () => ({ steps: STEPS }) };
    return { ok: true, json: async () => ({}) };
  });
  vi.stubGlobal("fetch", fetchMock);
});

describe("A correction pass inside one spec", () => {
  it("CorrectionPass_KeepsPassOneDone_AndSaysWhyPassTwoRuns", async () => {
    renderPage();

    await waitFor(() => expect(screen.getByTestId(`run-section-spec:${SPEC}`)).toHaveAttribute("data-state", "running"));
    expect(screen.getByTestId("run-step-22")).toHaveAttribute("data-state", "done");
    expect(screen.getByTestId("run-step-pass-22")).toHaveTextContent("Pass 1 of this spec · ended: nothing left to do");
    expect(screen.getByTestId("run-step-pass-25")).toHaveTextContent("Pass 2 of this spec · structure correction");
    expect(screen.getByTestId("run-step-pass-why-25")).toHaveTextContent("1 unit(s) over their Limits");
    expect(screen.getByTestId("run-step-pass-why-25")).toHaveTextContent("src/A.cs A.Long(): function_lines 70 > 55");
    // The checklist pass 1 finished is not a new start.
    expect(screen.getByTestId("run-section-plan")).toHaveAttribute("data-state", "done");
  });

  it("CorrectionPass_NowBlockFollowsPassTwo", async () => {
    renderPage();

    const now = await screen.findByTestId("run-now");
    await waitFor(() => expect(within(now).getAllByTestId("run-now-line")[0]).toHaveTextContent("Run master skill"));
    const pass = within(now).getAllByTestId("run-now-line").find((l) => l.getAttribute("data-kind") === "pass");
    expect(pass).toHaveTextContent("Pass 2 of this spec · structure correction");
  });
});
