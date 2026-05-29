import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { SubAgentTreeNode } from "../SubAgentTreeNode";

describe("SubAgentTreeNode", () => {
  const defaults = {
    subAgentId: "sa-1",
    name: "UploadHandlerAuditor",
    activity: "audit the upload handler for path-traversal",
    observationsCount: 4,
    findingsCount: 1,
    filesWrittenCount: 0,
    toolCalls: 7,
  };

  it("SubAgentTreeNode_RendersNameAndActivityBadges", () => {
    render(<SubAgentTreeNode {...defaults} />);
    expect(screen.getByTestId("sub-agent-name-sa-1")).toHaveTextContent("UploadHandlerAuditor");
    expect(screen.getByTestId("sub-agent-activity-sa-1")).toHaveTextContent(
      "audit the upload handler for path-traversal",
    );
  });

  it("SubAgentTreeNode_DisplaysDecisionAnchorCountChips", () => {
    render(<SubAgentTreeNode {...defaults} />);
    expect(screen.getByTestId("chip-obs-sa-1")).toHaveTextContent("4 obs");
    expect(screen.getByTestId("chip-findings-sa-1")).toHaveTextContent("1 find");
    expect(screen.getByTestId("chip-files-sa-1")).toHaveTextContent("0 files");
    expect(screen.getByTestId("chip-tools-sa-1")).toHaveTextContent("7 tools");
  });

  it("SubAgentTreeNode_PlacedAsChildOfMasterNode", () => {
    // The placement assertion: SubAgentTreeNode is a focusable + selectable
    // unit so a parent TrailTree row can render it as a nested child. The
    // contract here is that the component renders a button that surfaces
    // onSelect.
    const handler = vi.fn();
    render(<SubAgentTreeNode {...defaults} onSelect={handler} />);
    const node = screen.getByTestId("sub-agent-node-sa-1");
    expect(node.tagName).toBe("BUTTON");
    fireEvent.click(node);
    expect(handler).toHaveBeenCalledTimes(1);
  });
});
