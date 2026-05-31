import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ExecutionNode } from "../ExecutionNode";

const baseProps = {
  id: "n1",
  label: "Fetching ticket",
  status: "ok" as const,
  depth: 0,
  startSeconds: 0,
  durationSeconds: 1,
  totalSeconds: 10,
  durationLabel: "1.0s",
};

describe("ExecutionNode", () => {
  it("ExecutionNode_StatusDotMatchesStatus_OkFailRunWait", () => {
    const { rerender } = render(<ExecutionNode {...baseProps} />);
    expect(screen.getByTestId("status-dot-ok")).toBeInTheDocument();
    rerender(<ExecutionNode {...baseProps} status="fail" />);
    expect(screen.getByTestId("status-dot-fail")).toBeInTheDocument();
    rerender(<ExecutionNode {...baseProps} status="run" />);
    expect(screen.getByTestId("status-dot-run")).toBeInTheDocument();
    rerender(<ExecutionNode {...baseProps} status="wait" />);
    expect(screen.getByTestId("status-dot-wait")).toBeInTheDocument();
  });

  it("ExecutionNode_DurationLabel_RendersWhenSet", () => {
    render(<ExecutionNode {...baseProps} durationLabel="346ms" />);
    expect(screen.getByText("346ms")).toBeInTheDocument();
  });

  it("ExecutionNode_ClickingExpandableNode_OpensBody", () => {
    render(<ExecutionNode {...baseProps} body={<div>body-content</div>} />);
    expect(screen.queryByTestId("execution-node-n1-body")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("execution-node-n1-row"));
    expect(screen.getByTestId("execution-node-n1-body")).toBeInTheDocument();
    expect(screen.getByText("body-content")).toBeInTheDocument();
  });

  it("ExecutionNode_TailRendersUnderRowWhenSet", () => {
    render(
      <ExecutionNode
        {...baseProps}
        tail={{ text: "ticket #1 fetched", timestamp: "16:02:07" }}
      />,
    );
    const tail = screen.getByTestId("live-tail");
    expect(tail).toHaveTextContent("ticket #1 fetched");
    expect(tail).toHaveTextContent("16:02:07");
  });

  it("ExecutionNode_ChildrenRender_WhenExpanded", () => {
    const nested = [{ ...baseProps, id: "child1", label: "sub-agent: server", depth: 1 }];
    const propsWithChildren = { ...baseProps, children: nested, body: null };
    render(<ExecutionNode {...propsWithChildren} />);
    fireEvent.click(screen.getByTestId("execution-node-n1-row"));
    expect(screen.getByTestId("execution-node-child1")).toBeInTheDocument();
  });
});
