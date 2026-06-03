import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ExecutionNode } from "../ExecutionNode";

// p0203 (1) — handler's Message appears under the row instead of bare "done".

const baseProps = {
  id: "n1",
  label: "Run tests",
  status: "ok" as const,
  depth: 0,
  startSeconds: 0,
  durationSeconds: 1,
  totalSeconds: 10,
  durationLabel: "1.0s",
};

describe("ExecutionNode step-row outcome", () => {
  it("ExecutionNode_StepRow_RendersMessageInsteadOfBareDone", () => {
    render(<ExecutionNode {...baseProps} message="42 tests passed across 3 repos" />);
    const outcome = screen.getByTestId("step-outcome-n1-message");
    expect(outcome).toHaveTextContent("42 tests passed across 3 repos");
  });

  it("ExecutionNode_StepRow_OmitsOutcomeWhenMessageIsNull", () => {
    render(<ExecutionNode {...baseProps} message={null} />);
    expect(screen.queryByTestId("step-outcome-n1")).not.toBeInTheDocument();
  });

  it("ExecutionNode_StepRow_OmitsOutcomeWhenMessageIsEmptyString", () => {
    render(<ExecutionNode {...baseProps} message="" />);
    expect(screen.queryByTestId("step-outcome-n1")).not.toBeInTheDocument();
  });
});
