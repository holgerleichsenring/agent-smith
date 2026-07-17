import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { StoryBar } from "../StoryBar";
import type { RunBeats } from "@/types/hub-events";

// p0344b: the storybar renders SERVER-computed beat states verbatim — it holds
// no mapping logic of its own.

const beats: RunBeats = {
  ticket: "done",
  plan: "done",
  building: "active",
  verify: "pending",
  outcome: "failed",
};

describe("StoryBar", () => {
  it("StoryBar_ServerBeats_RendersFiveBeatsLeftToRight", () => {
    render(<StoryBar beats={beats} />);
    expect(screen.getByTestId("story-bar")).toBeInTheDocument();
    for (const key of ["ticket", "plan", "building", "verify", "outcome"]) {
      expect(screen.getByTestId(`story-beat-${key}`)).toBeInTheDocument();
    }
  });

  it("StoryBar_BeatState_ExposedOnDataAttributeAndCaption", () => {
    render(<StoryBar beats={beats} />);
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "active");
    expect(screen.getByTestId("story-beat-outcome")).toHaveAttribute("data-status", "failed");
    expect(screen.getByTestId("story-beat-outcome-caption")).toHaveTextContent("failed");
    expect(screen.getByTestId("story-beat-verify-caption")).toHaveTextContent("pending");
  });

  it("StoryBar_SkippedBeat_RendersMutedWithSkippedLabel", () => {
    render(<StoryBar beats={{ ...beats, verify: "skipped" }} />);
    const beat = screen.getByTestId("story-beat-verify");
    expect(beat).toHaveAttribute("data-status", "skipped");
    expect(screen.getByTestId("story-beat-verify-caption")).toHaveTextContent("skipped");
  });

  it("StoryBar_ClickBeat_InvokesCallbackWithBeatKey", () => {
    const onClick = vi.fn();
    render(<StoryBar beats={beats} onBeatClick={onClick} />);
    fireEvent.click(screen.getByTestId("story-beat-building"));
    expect(onClick).toHaveBeenCalledOnce();
    expect(onClick.mock.calls[0][0]).toBe("building");
  });
});
