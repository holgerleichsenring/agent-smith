import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ExecutionTree } from "../ExecutionTree";

describe("ExecutionTree", () => {
  it("ExecutionTree_RendersNodesInOrder_RespectsDepthIndent", () => {
    render(
      <ExecutionTree
        heading="Execution"
        totalSeconds={60}
        nodes={[
          {
            id: "a",
            label: "step A",
            status: "ok",
            depth: 0,
            startSeconds: 0,
            durationSeconds: 10,
            totalSeconds: 60,
            durationLabel: "10s",
          },
          {
            id: "b",
            label: "step B",
            status: "ok",
            depth: 0,
            startSeconds: 10,
            durationSeconds: 5,
            totalSeconds: 60,
            durationLabel: "5s",
          },
        ]}
      />,
    );
    expect(screen.getByTestId("execution-node-a")).toBeInTheDocument();
    expect(screen.getByTestId("execution-node-b")).toBeInTheDocument();
  });

  it("ExecutionTree_EmptyNodes_ShowsWaitingPlaceholder", () => {
    render(<ExecutionTree heading="Execution" totalSeconds={1} nodes={[]} />);
    expect(screen.getByText(/waiting for first step/i)).toBeInTheDocument();
  });
});
