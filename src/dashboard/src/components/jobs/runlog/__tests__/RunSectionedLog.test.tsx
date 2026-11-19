import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-18-d6d2e: the run page's log on its own — the run page and a ticket report draw it; the caller owns the open
// step, the ids' prefix and the ticket and check bodies.

vi.mock("@/hooks/useRunSpecs", () => ({ useRunSpecs: () => [] }));
vi.mock("@/hooks/useRunStepEvents", () => ({ useRunStepEvents: () => ({ events: [], hasOlder: false, loading: false, loadOlder: () => {} }) }));

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
