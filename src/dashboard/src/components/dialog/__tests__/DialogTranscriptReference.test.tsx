import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { DialogTranscript } from "../DialogTranscript";
import { withImages } from "../transcriptImages";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-10-01-283db: an uploaded website is ONE chip in the transcript, however many files it holds.
describe("DialogTranscript, with an uploaded website", () => {
  const said: DialogEntry = { key: "u-1", kind: "user", text: "here is our site", at: "2026-10-01T10:00:00Z" };
  const set = { setId: "s1", name: "landing", files: 42, bytes: 3 * 1024 * 1024, at: "2026-10-01T10:01:00Z" };

  it("transcript renders a site set as a chip with file count", () => {
    render(<DialogTranscript entries={withImages([said], [], [set])} />);

    const chip = screen.getByTestId("dialog-reference-s1");
    expect(chip).toHaveTextContent("landing");
    expect(chip).toHaveTextContent("42 files · 3.0 MB");
    const turns = screen.getAllByTestId(/dialog-turn-(user|reference)/).map((t) => t.dataset.testid);
    expect(turns).toEqual(["dialog-turn-user", "dialog-turn-reference"]);
  });

  // 2026-11-17-d762b: the upload is one plain line, its show files a text link — no card, no button box.
  it("transcript draws an upload as one line with a show files link", () => {
    const onOpen = vi.fn();
    render(<DialogTranscript entries={withImages([said], [], [set])} onOpenUpload={onOpen} />);

    const line = screen.getByTestId("dialog-reference-s1");
    expect(line.tagName).toBe("P");
    expect(line).toHaveTextContent(/^upload landing · 42 files · 3\.0 MB · show files$/);
    expect(line.closest(".ecard")).toBeNull();
    const open = screen.getByTestId("dialog-reference-open-s1");
    expect(open).toHaveClass("d-link");
    expect(open).not.toHaveClass("btn");
    fireEvent.click(open);
    expect(onOpen).toHaveBeenCalledWith("s1");
  });

  it("transcript names its speakers in words, not badges", () => {
    render(<DialogTranscript entries={[said]} />);
    expect(screen.getByTestId("dialog-turn-user").querySelector("[aria-hidden='true']")).toHaveTextContent(/^you$/);
  });
});
