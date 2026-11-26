import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-18-d6d2e: the run page's log on its own — the run page and a ticket report draw it; the caller owns the open
// step, the ids' prefix and the ticket and check bodies.

let specs: RunSpecRow[] = [];
vi.mock("@/hooks/useRunSpecs", () => ({ useRunSpecs: () => specs }));
vi.mock("@/hooks/useRunStepEvents", () => ({ useRunStepEvents: () => ({ events: [], hasOlder: false, loading: false, loadOlder: () => {} }) }));
const RECORD = "spec: s1\ngoal: g\nsteps:\n  - id: a\n    action: First\n  - id: b\n    action: Second\n";
vi.mock("@/lib/runSpecsApi", () => ({ fetchRunSpec: () => Promise.resolve({ spec: {}, record: RECORD }) }));

const { RunSectionedLog } = await import("../RunSectionedLog");

function row(stepIndex: number, over: Partial<RunStepRow> = {}): RunStepRow {
  return { stepIndex, stepName: "VerifySpec", displayName: "Build and test", commandName: "VerifySpecCommand", specId: "s1",
    status: "failed", durationSeconds: 3, resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 1, subAgents: 0, ...over };
}

const steps = [row(1)];

describe("RunSectionedLog", () => {
  it("RunSectionedLog_WithoutATicketBody_HasNoTicketSection_WithOneItHasIt", () => {
    // 2026-11-19-1b72c: the check section shows only for a run that recorded something — here its master's report.
    const reported = { acceptance: { criteria: [], outcome: null, ratifiedBy: null, reports: [{ markdown: "## Done" }] } } as unknown as RunSnapshot;
    const { rerender } = render(<RunSectionedLog runId="r1" snapshot={reported} events={[]} steps={steps} openStep={null}
      onOpenStep={() => {}} checkBody={<span data-testid="check-here" />} />);
    expect(screen.queryByTestId("run-section-ticket")).toBeNull();
    rerender(<RunSectionedLog runId="r1" snapshot={reported} events={[]} steps={steps} openStep={null}
      onOpenStep={() => {}} ticketBody={<span />} checkBody={<span data-testid="check-here" />} />);
    expect(screen.getByTestId("run-section-ticket")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("run-section-check")).getByRole("button", { expanded: false }));
    expect(screen.getByTestId("check-here")).toBeInTheDocument();
  });

  it("RunSectionedLog_AStepLine_AsksTheCallerToOpenIt_AndTheOpenStepShowsItsDetail", () => {
    const onOpenStep = vi.fn();
    const { rerender } = render(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={steps} openStep={null}
      onOpenStep={onOpenStep} checkBody={null} />);
    fireEvent.click(within(screen.getByTestId("run-step-1")).getByTestId("run-log-line"));
    expect(onOpenStep).toHaveBeenCalledWith(1);
    rerender(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={steps} openStep={1} onOpenStep={onOpenStep} checkBody={null} />);
    expect(screen.getByTestId("run-step-detail-1")).toBeInTheDocument();
  });

  it("RunSectionedLog_AStepClickedInASectionThePersonOpened_KeepsTheSectionOpenAndShowsTheDetail", () => {
    const done = [row(1, { status: "success" })];
    const { rerender } = render(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={done} openStep={null}
      onOpenStep={() => {}} checkBody={null} />);
    expect(screen.queryByTestId("run-step-1")).toBeNull();
    fireEvent.click(within(screen.getByTestId("run-section-spec:s1")).getByRole("button", { expanded: false }));
    fireEvent.click(within(screen.getByTestId("run-step-1")).getByTestId("run-log-line"));
    rerender(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={done} openStep={1}
      onOpenStep={() => {}} checkBody={null} />);
    expect(screen.getByTestId("run-step-1")).toBeInTheDocument();
    expect(screen.getByTestId("run-step-detail-1")).toBeInTheDocument();
  });

  it("RunSectionedLog_AStepNamedOnArrival_OpensItsSection", () => {
    render(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={[row(1, { status: "success" })]} openStep={1}
      onOpenStep={() => {}} checkBody={null} />);
    expect(screen.getByTestId("run-step-detail-1")).toBeInTheDocument();
  });

  it("RunSectionedLog_ASpecRevisedInPass2_ShowsItsStepsOnceAtTheSpecWithTheRevision", async () => {
    // 2026-11-26-5201g: the steps were drawn under every opened master pass, each as the spec stood at the end.
    const master = (stepIndex: number) => row(stepIndex, { stepName: "AgenticMaster", displayName: "Run master skill", commandName: "AgenticMasterCommand", status: "success" });
    const passes = [master(1), row(2, { status: "success", commandName: "MasterOpenQuestionsCommand", stepName: "MasterOpenQuestions" }), master(3)];
    specs = [{ specId: "s1", ordinal: 1, title: "t", status: "in_progress", startedAt: "2026-11-26T10:00:00Z", endedAt: null, verdict: null, steps: [],
      decisions: [{ stepIndex: 3, name: "Spec s1 revised in run r1 as revision 2: the operator narrowed it", reason: "revise-spec", category: "Implementation", recordedAt: "2026-11-26T11:00:00Z" }] }];
    try {
      render(<RunSectionedLog runId="r1" snapshot={null} events={[]} steps={passes} openStep={1} onOpenStep={() => {}} checkBody={null}
        passes={{ data: null, loading: false, failed: false }} />);

      expect(await screen.findAllByTestId("step-spec-step")).toHaveLength(2);
      expect(screen.getAllByTestId("step-part-steps")).toHaveLength(1);
      expect(within(screen.getByTestId("run-step-detail-1")).queryByTestId("step-part-steps")).toBeNull();
      expect(within(screen.getByTestId("spec-body-s1")).getByTestId("step-part-steps")).toBeInTheDocument();
      expect(screen.getByTestId("step-spec-revision")).toHaveTextContent("revised in pass 2 — Spec s1 revised in run r1 as revision 2: the operator narrowed it");
    } finally {
      specs = [];
    }
  });

  it("RunSectionedLog_TwoLogsWithTheirPrefixes_ShareNoElementId", () => {
    const { container } = render(
      <>
        <RunSectionedLog runId="r1" snapshot={null} events={[]} steps={steps} openStep={null} onOpenStep={() => {}} idPrefix="r-r1-" checkBody={null} />
        <RunSectionedLog runId="r2" snapshot={null} events={[]} steps={steps} openStep={null} onOpenStep={() => {}} idPrefix="r-r2-" checkBody={null} />
      </>,
    );
    const ids = [...container.querySelectorAll("[id]")].map((e) => e.id);
    expect(ids.length).toBeGreaterThan(2);
    expect(new Set(ids).size).toBe(ids.length);
  });
});
