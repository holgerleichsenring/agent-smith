import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogTranscript } from "../DialogTranscript";
import { withImages } from "../transcriptImages";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-10-01-283db: an uploaded website is ONE chip in the transcript, however many files it holds.
describe("DialogTranscript, with an uploaded website", () => {
  const said: DialogEntry = { key: "u-1", kind: "user", text: "here is our site", at: "2026-10-01T10:00:00Z" };
  const set = { setId: "s1", name: "landing", files: 42, bytes: 3 * 1024 * 1024, at: "2026-10-01T10:01:00Z" };

  it("transcript renders a site set as a chip with file count", () => {
    render(<DialogTranscript entries={withImages([said], [], [set])} onInspect={() => {}} />);

    const chip = screen.getByTestId("dialog-reference-s1");
    expect(chip).toHaveTextContent("landing");
    expect(chip).toHaveTextContent("42 files · 3.0 MB");
    const turns = screen.getAllByTestId(/dialog-turn-(user|reference)/).map((t) => t.dataset.testid);
    expect(turns).toEqual(["dialog-turn-user", "dialog-turn-reference"]);
    expect(screen.queryByTestId("dialog-reference-note-s1")).not.toBeInTheDocument();
  });

  // 2026-10-02-075dd: the note the model recorded is shown on the chip, folded.
  it("transcript shows a set's note on its chip", () => {
    const noted = { ...set, note: "Flask app. Run: python3 -m venv /tmp/v && /tmp/v/bin/pip install flask" };
    render(<DialogTranscript entries={withImages([said], [], [noted])} onInspect={() => {}} />);

    expect(screen.getByTestId("dialog-reference-note-s1")).toHaveTextContent("Flask app. Run:");
  });
});
