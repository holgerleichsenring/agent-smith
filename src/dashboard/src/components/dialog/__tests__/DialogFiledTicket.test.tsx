import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogFiledPanel } from "../DialogFiledPanel";
import type { SpecDialogFilingPush } from "@/types/spec-dialog";

// 2026-10-14-dbcel: a ticket of a filed list that waits for the tickets it requires is HELD — the
// panel says it waits, and why, rather than claiming it started or failed to.
describe("DialogFiledTicket, for a held ticket of a filed list", () => {
  it("shows waits with the reason", () => {
    const filed: SpecDialogFilingPush = {
      dialogId: "d-1",
      filed: [
        {
          reference: "https://tracker.test/2",
          title: "Use the new endpoint",
          key: "#2",
          start: { state: "Held", reason: "waits for #1 to be merged or marked done by hand" },
        },
      ],
      error: null,
      at: "2026-10-14T09:00:00Z",
    };

    render(<DialogFiledPanel filed={filed} work={null} />);

    const start = screen.getByTestId("dialog-filed-start-https://tracker.test/2").textContent;
    expect(start).toContain("waits");
    expect(start).toContain("waits for #1 to be merged or marked done by hand");
  });
});
