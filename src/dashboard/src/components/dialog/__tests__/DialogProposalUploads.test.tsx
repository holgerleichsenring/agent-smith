import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DialogProposalUploads } from "../DialogProposalUploads";

// 2026-11-04-96a0c: per upload, what the proposing turn and the conversation opened of the readable files.
describe("DialogProposalUploads", () => {
  it("DialogProposalUploads: renders opened of total per upload and nothing for an older proposal", () => {
    const { container, rerender } = render(<DialogProposalUploads uploads={[
      { address: "reference:app", setId: "a", thisTurn: 6, opened: 60, files: 66 },
      { address: "reference:docs", setId: "b", thisTurn: 0, opened: 0, files: 3 },
    ]} />);

    expect(screen.getAllByTestId("dialog-proposal-upload").map((line) => line.textContent)).toEqual([
      "reference:app — this turn opened 6; the conversation 60 of 66",
      "reference:docs — this turn opened 0; the conversation 0 of 3",
    ]);

    rerender(<DialogProposalUploads uploads={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });
});
