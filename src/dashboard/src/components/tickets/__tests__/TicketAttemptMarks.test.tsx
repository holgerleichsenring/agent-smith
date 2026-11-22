import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MAX_MARKS, TicketAttemptMarks } from "../TicketAttemptMarks";

// 2026-11-22-9719b: a long history keeps its column — the newest marks are drawn, the older ones fold into "+N".
const marks = (n: number) => Array.from({ length: n }, (_, i) => ({ runId: `r${i}`, status: "failed", endedOnInfrastructure: false, handedBack: false }));

describe("TicketAttemptMarks", () => {
  it("TicketAttemptMarks_FortyThreeAttempts_DrawsTheNewestSixAndFoldsTheRest", () => {
    render(<TicketAttemptMarks marks={marks(43)} count={43} />);
    const strip = screen.getByTestId("ticket-attempts");
    expect(strip.querySelectorAll("i")).toHaveLength(MAX_MARKS);
    expect(strip.querySelector("i")?.getAttribute("title")).toMatch(/^r37 /);
    expect(screen.getByTestId("ticket-attempts-folded")).toHaveTextContent("+37");
    expect(strip).toHaveTextContent(/43$/);
  });

  it("TicketAttemptMarks_AFewAttempts_FoldNothing", () => {
    render(<TicketAttemptMarks marks={marks(3)} count={3} />);
    expect(screen.getByTestId("ticket-attempts").querySelectorAll("i")).toHaveLength(3);
    expect(screen.queryByTestId("ticket-attempts-folded")).toBeNull();
  });
});
