import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { actLines, DialogTranscript } from "../DialogTranscript";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-11-26-f511c: the series chat's turns carry their time, a turn within a minute of the one before shows none; a series
// beat's acts are one small line under the master's sentence.

const base = Date.now() - 3_600_000;
const at = (minutes: number, seconds = 0) => new Date(base + minutes * 60_000 + seconds * 1000).toISOString();
const turn = (key: string, kind: "user" | "agent", text: string, when: string): DialogEntry => ({ key, kind, text, at: when });

describe("DialogTranscript, with times", () => {
  it("DialogTranscript_Times_EachTurnHasItsTime_ATurnWithinAMinuteNone_TheFullDateOnHover", () => {
    render(<DialogTranscript times entries={[
      turn("u-1", "user", "How is #19667?", at(0)),
      turn("a-1", "agent", "It is being refreshed.", at(0, 30)),
      turn("a-2", "agent", "Done.", at(5)),
    ]} />);

    const times = screen.getAllByTestId("dialog-turn-time");
    expect(times).toHaveLength(2);
    expect(times[0]).toHaveAttribute("dateTime", at(0));
    expect(times[0].getAttribute("title")).toMatch(/\d{4}/);
    expect(times[0].textContent).toMatch(/^\d{2}:\d{2}$|^\w+ \d{2}:\d{2}$/);
  });

  it("DialogTranscript_WithoutTimes_ShowsNone", () => {
    render(<DialogTranscript entries={[turn("u-1", "user", "Hi", at(0))]} />);

    expect(screen.queryByTestId("dialog-turn-time")).toBeNull();
  });

  it("DialogTranscript_ABeatsActs_AreOneLineUnderTheMastersSentence", () => {
    render(<DialogTranscript entries={[
      turn("a-1", "agent", "#19667 is brought up to date with develop.\n\n↳ refreshed #19667 · started the CI of #19659", at(0)),
    ]} />);

    expect(screen.getByText("#19667 is brought up to date with develop.")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-turn-acts")).toHaveTextContent("↳ refreshed #19667 · started the CI of #19659");
  });
});

describe("actLines", () => {
  it("ActLines_SplitsTheTrailingArrowLines_AndLeavesEveryOtherTextWhole", () => {
    expect(actLines("Sentence.\n\n↳ refreshed #1 · retried #2")).toEqual({ body: "Sentence.", acts: ["↳ refreshed #1 · retried #2"] });
    expect(actLines("No acts here.")).toEqual({ body: "No acts here.", acts: [] });
    expect(actLines("↳ only an arrow")).toEqual({ body: "↳ only an arrow", acts: [] });
    expect(actLines("↳ first\nthen text")).toEqual({ body: "↳ first\nthen text", acts: [] });
  });
});
