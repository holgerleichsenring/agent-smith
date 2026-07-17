import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { StoryBar, BEAT_ORDER, type BeatKey } from "../StoryBar";
import type { RunBeats } from "@/types/hub-events";

// p0344b: the storybar renders SERVER-computed beat states verbatim — it holds
// no mapping logic of its own. p0343c: it emits the run-viewer mock's .beat DOM
// (marker/bt/bs, s-* state classes); the parent supplies the real per-beat sub
// captions and the selected beat (aria-current).

const beats: RunBeats = {
  ticket: "done",
  plan: "done",
  building: "active",
  verify: "pending",
  outcome: "failed",
};

const subs = Object.fromEntries(
  BEAT_ORDER.map((k) => [k, `${k}-sub`]),
) as Record<BeatKey, string>;

describe("StoryBar", () => {
  it("StoryBar_ServerBeats_RendersFiveBeatsLeftToRight", () => {
    render(<StoryBar beats={beats} subs={subs} selected="building" />);
    expect(screen.getByTestId("story-bar")).toBeInTheDocument();
    for (const key of ["ticket", "plan", "building", "verify", "outcome"]) {
      expect(screen.getByTestId(`story-beat-${key}`)).toBeInTheDocument();
    }
  });

  it("StoryBar_BeatState_ExposedOnDataAttributeAndMockClass", () => {
    render(<StoryBar beats={beats} subs={subs} selected="building" />);
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "active");
    expect(screen.getByTestId("story-beat-building").className).toContain("s-run");
    expect(screen.getByTestId("story-beat-outcome")).toHaveAttribute("data-status", "failed");
    expect(screen.getByTestId("story-beat-outcome").className).toContain("s-fail");
    expect(screen.getByTestId("story-beat-verify").className).toContain("s-idle");
    expect(screen.getByTestId("story-beat-ticket").className).toContain("s-done");
  });

  it("StoryBar_Markers_DoneCheck_FailedCross_ActiveEmpty", () => {
    render(<StoryBar beats={beats} subs={subs} selected="building" />);
    const marker = (key: string) =>
      screen.getByTestId(`story-beat-${key}`).querySelector(".marker")!;
    expect(marker("ticket")).toHaveTextContent("✓");
    expect(marker("plan")).toHaveTextContent("✓");
    expect(marker("building").textContent).toBe("");
    expect(marker("outcome")).toHaveTextContent("✗");
  });

  it("StoryBar_PausedRun_ActiveBeatGetsWaitLookAndQuestionMarker", () => {
    render(<StoryBar beats={beats} subs={subs} selected="building" paused />);
    const building = screen.getByTestId("story-beat-building");
    expect(building.className).toContain("s-wait");
    expect(building.querySelector(".marker")).toHaveTextContent("?");
  });

  it("StoryBar_Subs_RenderedPerBeat_FromParentDerivation", () => {
    render(<StoryBar beats={beats} subs={{ ...subs, building: "Step 3 of 7" }} selected="building" />);
    expect(screen.getByTestId("story-beat-building-caption")).toHaveTextContent("Step 3 of 7");
    expect(screen.getByTestId("story-beat-ticket-caption")).toHaveTextContent("ticket-sub");
  });

  it("StoryBar_SelectedBeat_CarriesAriaCurrent", () => {
    render(<StoryBar beats={beats} subs={subs} selected="verify" />);
    expect(screen.getByTestId("story-beat-verify")).toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("aria-current", "false");
  });

  it("StoryBar_ClickBeat_InvokesCallbackWithBeatKey", () => {
    const onClick = vi.fn();
    render(<StoryBar beats={beats} subs={subs} selected="building" onBeatClick={onClick} />);
    fireEvent.click(screen.getByTestId("story-beat-building"));
    expect(onClick).toHaveBeenCalledOnce();
    expect(onClick.mock.calls[0][0]).toBe("building");
  });
});
