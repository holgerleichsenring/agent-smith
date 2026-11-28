import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DialogTranscript } from "../DialogTranscript";
import { documentBlocks } from "../documentBlocks";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-11-28-d1ffc: the series check says a few lines and keeps its evidence in a ````details fence, drawn collapsed behind
// "show all".
describe("DialogTranscript, with a details part in an agent turn", () => {
  const text = "All 4 tickets landed. Series check on main: 6 of 8 hold, 2 not checkable here.\n\nNothing waits on you.\n\n"
    + "````details\nFailing:\n- #1 · s-1: requests persist — dotnet test: a long stack trace\n````";
  const entry: DialogEntry = { key: "a-1", kind: "agent", text, at: "2026-11-28T10:00:00Z" };

  it("splits a details fence apart from the prose", () => {
    expect(documentBlocks(text).map((part) => part.kind)).toEqual(["prose", "details"]);
  });

  it("shows the short lines and keeps the details behind show all", () => {
    render(<DialogTranscript entries={[entry]} />);

    expect(screen.getByText("Nothing waits on you.")).toBeInTheDocument();
    expect(screen.queryByText(/a long stack trace/)).toBeNull();
    fireEvent.click(screen.getByTestId("dialog-details-toggle"));
    expect(screen.getByTestId("dialog-details-body")).toHaveTextContent("a long stack trace");
    expect(screen.getByTestId("dialog-details-toggle")).toHaveTextContent("show less");
  });
});
