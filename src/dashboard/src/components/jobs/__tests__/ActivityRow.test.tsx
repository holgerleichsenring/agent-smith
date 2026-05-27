import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ActivityRow } from "../ActivityRow";
import {
  EventType,
  type CatalogIssueEvent,
  type GateCheckedEvent,
  type StepFinishedEvent,
  type ToolResultEvent,
} from "@/types/hub-events";

const RUN_ID = "2026-05-27T18-00-00-cccc";
const TS = "2026-05-27T18:00:00.000Z";

describe("ActivityRow", () => {
  it("StepFinished failed with Reason shows reason inline and uses error styling", () => {
    const event: StepFinishedEvent = {
      runId: RUN_ID,
      type: EventType.StepFinished,
      timestamp: TS,
      stepIndex: 4,
      status: "failed",
      durationMs: 250,
      reason: "BootstrapDiscover: no skill with output_schema=discovery available",
    };
    render(<ActivityRow event={event} expanded={false} onToggle={vi.fn()} />);
    const row = screen.getByTestId(`activity-row-${EventType.StepFinished}`);
    expect(row).toHaveAttribute("data-severity", "error");
    expect(screen.getByText(/no skill with output_schema=discovery/)).toBeInTheDocument();
  });

  it("GateChecked passed=false with reason shows reason inline as error", () => {
    const event: GateCheckedEvent = {
      runId: RUN_ID,
      type: EventType.GateChecked,
      timestamp: TS,
      gate: "EmptyPlanCheck",
      passed: false,
      reason: "plan has no executable steps",
    };
    render(<ActivityRow event={event} expanded={false} onToggle={vi.fn()} />);
    const row = screen.getByTestId(`activity-row-${EventType.GateChecked}`);
    expect(row).toHaveAttribute("data-severity", "error");
    expect(screen.getByText(/plan has no executable steps/)).toBeInTheDocument();
  });

  it("ToolResult ok=false with errorMessage shows message inline as warn", () => {
    const event: ToolResultEvent = {
      runId: RUN_ID,
      type: EventType.ToolResult,
      timestamp: TS,
      tool: "read_file",
      ok: false,
      resultLength: 0,
      errorMessage: "file not found: /work/missing.txt",
    };
    render(<ActivityRow event={event} expanded={false} onToggle={vi.fn()} />);
    const row = screen.getByTestId(`activity-row-${EventType.ToolResult}`);
    expect(row).toHaveAttribute("data-severity", "warn");
    expect(screen.getByText(/file not found/)).toBeInTheDocument();
  });

  it("CatalogIssue warning renders amber (warn severity)", () => {
    const event: CatalogIssueEvent = {
      runId: RUN_ID,
      type: EventType.CatalogIssue,
      timestamp: TS,
      severity: "warning",
      source: "skills/project-discovery/SKILL.md",
      category: "skill-validation",
      message: "description must be at most 200 chars",
    };
    render(<ActivityRow event={event} expanded={false} onToggle={vi.fn()} />);
    const row = screen.getByTestId(`activity-row-${EventType.CatalogIssue}`);
    expect(row).toHaveAttribute("data-severity", "warn");
    expect(screen.getByText(/description must be at most 200 chars/)).toBeInTheDocument();
  });

  it("CatalogIssue error renders rose (error severity)", () => {
    const event: CatalogIssueEvent = {
      runId: RUN_ID,
      type: EventType.CatalogIssue,
      timestamp: TS,
      severity: "error",
      source: "skills/concept-vocabulary.yaml",
      category: "vocabulary-parse",
      message: "legacy three-section shape",
    };
    render(<ActivityRow event={event} expanded={false} onToggle={vi.fn()} />);
    const row = screen.getByTestId(`activity-row-${EventType.CatalogIssue}`);
    expect(row).toHaveAttribute("data-severity", "error");
  });

  it("click invokes onToggle for expand affordance", () => {
    const event: StepFinishedEvent = {
      runId: RUN_ID,
      type: EventType.StepFinished,
      timestamp: TS,
      stepIndex: 1,
      status: "success",
      durationMs: 100,
      reason: null,
    };
    const onToggle = vi.fn();
    render(<ActivityRow event={event} expanded={false} onToggle={onToggle} />);
    fireEvent.click(screen.getByRole("button"));
    expect(onToggle).toHaveBeenCalledOnce();
  });
});
