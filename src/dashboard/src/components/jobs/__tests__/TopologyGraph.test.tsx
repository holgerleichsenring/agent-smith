import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { TopologyGraph } from "../TopologyGraph";
import { EventType, type RunEvent } from "@/types/hub-events";

const RUN_ID = "2026-05-27T20-30-00-ffff";
const TS = "2026-05-27T20:30:00.000Z";

function created(repo: string): RunEvent {
  return {
    runId: RUN_ID,
    type: EventType.SandboxCreated,
    timestamp: TS,
    repo,
    image: "img:latest",
    language: "csharp",
  };
}

describe("TopologyGraph", () => {
  it("renders no-sandboxes empty state when events list is empty", () => {
    render(<TopologyGraph pipeline="fix-bug" runId={RUN_ID} events={[]} selected={null} onSelect={vi.fn()} />);
    expect(screen.getByTestId("topology-graph-empty")).toBeInTheDocument();
  });

  it("five sandboxes renders root + five sandbox nodes + five edges", () => {
    const events = ["a", "b", "c", "d", "e"].map(created);
    render(<TopologyGraph pipeline="fix-bug" runId={RUN_ID} events={events} selected={null} onSelect={vi.fn()} />);
    expect(screen.getByTestId("topology-node-run")).toBeInTheDocument();
    for (const repo of ["a", "b", "c", "d", "e"]) {
      expect(screen.getByTestId(`topology-node-sandbox-${repo}`)).toBeInTheDocument();
    }
    expect(screen.getAllByTestId("topology-edge")).toHaveLength(5);
  });

  it("nine sandboxes lays out across two rows (no library, pure math)", () => {
    const events = ["a", "b", "c", "d", "e", "f", "g", "h", "i"].map(created);
    render(<TopologyGraph pipeline="fix-bug" runId={RUN_ID} events={events} selected={null} onSelect={vi.fn()} />);
    // 9th node's y coordinate must be the row-2 y, not row-1's
    const last = screen.getByTestId("topology-node-sandbox-i");
    const rect = last.querySelector("rect");
    expect(rect).not.toBeNull();
    // Row 2 y starts at 280 - half-height = 252; row 1 starts at 200 - 28 = 172
    const y = Number(rect!.getAttribute("y"));
    expect(y).toBeGreaterThan(200);
  });

  it("clicking a sandbox node fires onSelect with the repo name", () => {
    const onSelect = vi.fn();
    render(<TopologyGraph pipeline="fix-bug" runId={RUN_ID} events={[created("server")]} selected={null} onSelect={onSelect} />);
    fireEvent.click(screen.getByTestId("topology-node-sandbox-server"));
    expect(onSelect).toHaveBeenCalledWith("server");
  });

  it("selected prop highlights the node via data-selected", () => {
    const events = [created("server"), created("client")];
    render(<TopologyGraph pipeline="fix-bug" runId={RUN_ID} events={events} selected="server" onSelect={vi.fn()} />);
    expect(screen.getByTestId("topology-node-sandbox-server")).toHaveAttribute("data-selected", "true");
    expect(screen.getByTestId("topology-node-sandbox-client")).toHaveAttribute("data-selected", "false");
  });
});
