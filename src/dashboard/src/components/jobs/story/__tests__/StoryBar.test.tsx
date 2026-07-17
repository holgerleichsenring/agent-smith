import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { StoryBar } from "../StoryBar";
import type { Beat } from "../beatMapping";

function beats(): Beat[] {
  return [
    { key: "ticket", label: "Ticket", index: 1, status: "done", stepIds: ["s-t"], anchorId: "s-t" },
    { key: "plan", label: "Plan", index: 2, status: "done", stepIds: ["s-p"], anchorId: "s-p" },
    { key: "building", label: "Building", index: 3, status: "active", stepIds: ["s-b"], anchorId: "s-b" },
    { key: "verify", label: "Verify", index: 4, status: "idle", stepIds: [], anchorId: null },
    { key: "outcome", label: "Outcome", index: 5, status: "fail", stepIds: ["s-o"], anchorId: "s-o" },
  ];
}

describe("StoryBar", () => {
  it("StoryBar_RendersFiveBeats_LeftToRight", () => {
    render(<StoryBar beats={beats()} />);
    expect(screen.getByTestId("story-bar")).toBeInTheDocument();
    for (const key of ["ticket", "plan", "building", "verify", "outcome"]) {
      expect(screen.getByTestId(`story-beat-${key}`)).toBeInTheDocument();
    }
  });

  it("StoryBar_BeatStatus_ExposedOnDataAttribute", () => {
    render(<StoryBar beats={beats()} />);
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "active");
    expect(screen.getByTestId("story-beat-outcome")).toHaveAttribute("data-status", "fail");
    expect(screen.getByTestId("story-beat-outcome-caption")).toHaveTextContent("failed");
  });

  it("StoryBar_ClickBeat_InvokesCallbackWithBeat", () => {
    const onClick = vi.fn();
    render(<StoryBar beats={beats()} onBeatClick={onClick} />);
    fireEvent.click(screen.getByTestId("story-beat-building"));
    expect(onClick).toHaveBeenCalledOnce();
    expect(onClick.mock.calls[0][0].key).toBe("building");
  });
});
