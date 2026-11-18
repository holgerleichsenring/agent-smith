import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";
import { RunLog } from "../RunLog";
import { runLogSections } from "../runLogSections";
import { RunStepHead } from "../RunStepHead";

// 2026-11-17-6d9eb: the sectioned log as a person meets it — section and step lines with llm · cost · time, a section
// toggles, the mechanics open in place, a step opens its head in place.

function row(stepIndex: number, commandName: string, over: Partial<RunStepRow> = {}): RunStepRow {
  return {
    stepIndex, stepName: commandName, displayName: commandName, commandName, status: "success", durationSeconds: 10,
    resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over,
  };
}

const snapshot = {
  runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "success", prUrl: null, summary: null,
  startedAt: "2026-10-13T06:00:00Z", finishedAt: "2026-10-13T07:00:00Z", sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 4,
  lastEventType: null, costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
} as RunSnapshot;

const SPEC = "2026-10-07-1891a";
const steps = [
  row(1, "FetchTicket", { displayName: "Fetch ticket", durationSeconds: 1.5 }),
  row(2, "AgenticMaster", { specId: SPEC, displayName: "Run master skill", llmCalls: 168, costUsd: 7.26, durationSeconds: 4510,
    resultMessage: "Pull request created: https://example.test/a/pull/9", time: { modelMs: 3_600_000, sandboxMs: 600_000, throttleMs: 0, scaffoldingMs: 310_000 } }),
  row(3, "LoadContext", { specId: SPEC, displayName: "Load context", stepClass: "internal" }),
];

function Harness({ body = vi.fn(() => null) }: { body?: () => null }) {
  const sections = runLogSections(steps, [], snapshot, null);
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [step, setStep] = useState<number | null>(null);
  const [mech, setMech] = useState<Set<string>>(new Set());
  const flip = (s: Set<string>, id: string) => { const n = new Set(s); if (!n.delete(id)) n.add(id); return n; };
  return (
    <RunLog sections={sections} events={[]} open={open} onToggle={(id) => setOpen((s) => flip(s, id))}
      openStep={step} onToggleStep={(i) => setStep((n) => (n === i ? null : i))}
      mechanicsOpen={mech} onToggleMechanics={(id) => setMech((s) => flip(s, id))}
      body={body} detail={(s) => <RunStepHead step={s} />} />
  );
}

describe("RunLog", () => {
  it("RunLog_SectionLine_SaysStepsLlmCostTime_AndToggles", () => {
    render(<Harness />);
    const spec = screen.getByTestId(`run-section-spec:${SPEC}`);
    expect(spec).toHaveTextContent(`Spec ${SPEC}`);
    expect(screen.getByTestId(`run-section-meta-spec:${SPEC}`)).toHaveTextContent("2 steps · 168 llm · $7.26 · 1h 15m");
    expect(within(spec).queryAllByTestId("run-log-line")).toHaveLength(0);
    fireEvent.click(within(spec).getByRole("button", { expanded: false }));
    const line = within(spec).getAllByTestId("run-log-line")[0];
    expect(line).toHaveTextContent("Run master skill168 llm$7.2675m 10s");
    fireEvent.click(within(spec).getAllByRole("button", { expanded: true })[0]);
    expect(within(spec).queryAllByTestId("run-log-line")).toHaveLength(0);
  });

  it("RunLog_Mechanics_OpenInPlace", () => {
    render(<Harness />);
    const spec = screen.getByTestId(`run-section-spec:${SPEC}`);
    fireEvent.click(within(spec).getByRole("button", { expanded: false }));
    expect(within(spec).getAllByTestId("run-log-line")).toHaveLength(1);
    fireEvent.click(screen.getByTestId(`run-mechanics-spec:${SPEC}`));
    expect(within(spec).getAllByTestId("run-log-line")).toHaveLength(2);
    expect(within(spec).getAllByTestId("run-log-line")[1]).toHaveTextContent("Load context");
  });

  it("RunLog_AStepOpensItsDetailInPlace_WithItsMessageLinked", () => {
    render(<Harness />);
    const spec = screen.getByTestId(`run-section-spec:${SPEC}`);
    fireEvent.click(within(spec).getByRole("button", { expanded: false }));
    fireEvent.click(within(spec).getAllByTestId("run-log-line")[0]);
    const detail = screen.getByTestId("run-step-detail-2");
    expect(within(detail).getByTestId("step-head")).toHaveTextContent("done · 60m 0s model · 10m 0s sandbox · 5m 10s scaffolding");
    expect(within(detail).getByRole("link")).toHaveAttribute("href", "https://example.test/a/pull/9");
  });

  it("RunLog_AnOpenSection_DrawsItsBody", () => {
    const body = vi.fn(() => null);
    render(<Harness body={body} />);
    fireEvent.click(within(screen.getByTestId("run-section-ticket")).getByRole("button"));
    expect(body).toHaveBeenCalledWith(expect.objectContaining({ id: "ticket", kind: "ticket" }));
  });
});
