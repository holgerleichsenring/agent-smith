import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { DetailPane } from "../DetailPane";
import type { ExecutionNodeProps } from "../ExecutionNode";

function node(over: Partial<ExecutionNodeProps> & { id: string; label: string }): ExecutionNodeProps {
  return {
    status: "ok", depth: 0, startSeconds: 0, durationSeconds: 1, totalSeconds: 10,
    durationLabel: "1.0s", ...over,
  };
}

describe("DetailPane", () => {
  it("DetailPane_FailedNode_RendersFailPillAndMessage", () => {
    const n = node({
      id: "step-14", label: "Run tests", status: "fail",
      durationLabel: "3m13s", message: "Tests failed — exit -1 · sandbox disposed",
    });
    render(<DetailPane node={n} parentLabel={null} />);

    expect(screen.getByTestId("detail-pane-title")).toHaveTextContent("Run tests");
    expect(screen.getByTestId("detail-pane-pill")).toHaveTextContent("failed");
    expect(screen.getByTestId("detail-pane-message")).toHaveTextContent("exit -1");
  });

  it("DetailPane_NodeWithBody_RendersBody", () => {
    const n = node({
      id: "step-9", label: "Analyze codebase",
      body: <div data-testid="probe-body">event stream here</div>,
    });
    render(<DetailPane node={n} parentLabel={null} />);

    expect(screen.getByTestId("probe-body")).toBeInTheDocument();
  });

  it("DetailPane_ChildNode_RendersParentCrumb", () => {
    const n = node({ id: "sub-x", label: "sub-agent: x", depth: 1 });
    render(<DetailPane node={n} parentLabel="Analyze codebase" />);

    expect(screen.getByTestId("detail-pane")).toHaveTextContent("Analyze codebase ›");
  });

  it("DetailPane_NoSelection_RendersPrompt", () => {
    render(<DetailPane node={null} parentLabel={null} />);

    expect(screen.getByTestId("detail-pane")).toHaveTextContent("Select a step");
  });
});
