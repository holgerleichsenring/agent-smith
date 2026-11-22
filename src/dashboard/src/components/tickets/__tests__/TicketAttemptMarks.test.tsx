import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { TicketAttemptMark } from "@/types/tickets";
import { MAX_MARKS, markKind, markTitle, TicketAttemptMarks } from "../TicketAttemptMarks";

// 2026-11-22-9719b: a long history keeps its column — the newest marks are drawn, the older ones fold into "+N".
// 2026-11-22-50d7a: each mark says its number, how it began and how it ended; the group says what the squares are.
const marks = (n: number): TicketAttemptMark[] =>
  Array.from({ length: n }, (_, i) => ({ runId: `r${i}`, status: "failed", endedOnInfrastructure: false, handedBack: false }));
const en = translator("en");

describe("TicketAttemptMarks", () => {
  it("TicketAttemptMarks_FortyThreeAttempts_DrawsTheNewestSixAndFoldsTheRest", () => {
    render(<TicketAttemptMarks marks={marks(43)} count={43} />);
    const strip = screen.getByTestId("ticket-attempts");
    expect(strip.querySelectorAll("i")).toHaveLength(MAX_MARKS);
    expect(strip.querySelector("i")?.getAttribute("title")).toBe("Attempt 38 · failed");
    expect(screen.getByTestId("ticket-attempts-folded")).toHaveTextContent("+37");
    expect(screen.getByTestId("ticket-attempts-folded").getAttribute("title")).toBe("37 older attempts");
    expect(strip).toHaveTextContent(/43$/);
  });

  it("TicketAttemptMarks_AFewAttempts_FoldNothing", () => {
    render(<TicketAttemptMarks marks={marks(3)} count={3} />);
    expect(screen.getByTestId("ticket-attempts").querySelectorAll("i")).toHaveLength(3);
    expect(screen.queryByTestId("ticket-attempts-folded")).toBeNull();
  });

  // 2026-11-22-7103a: a shortfall left a spec undelivered — its own kind, red, never the green of a success.
  it("MarkKind_Shortfall_IsShortfallRed", () => {
    expect(markKind({ runId: "r1", status: "shortfall", endedOnInfrastructure: false, handedBack: false })).toBe("shortfall");
    render(<TicketAttemptMarks marks={[{ runId: "r1", status: "shortfall", endedOnInfrastructure: false, handedBack: false }]} count={1} />);
    const mark = screen.getByTestId("ticket-attempts").querySelector("i")!;
    expect(mark).toHaveClass("bad");
    expect(mark.getAttribute("title")).toBe("Attempt 1 · done with a shortfall");
  });

  it("TicketAttemptMarks_Group_SaysWhatTheSquaresAre", () => {
    render(<TicketAttemptMarks marks={marks(2)} count={2} />);
    expect(screen.getByRole("group", { name: "2 attempts, one square each, oldest first" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Attempt 2 · failed" })).toBeInTheDocument();
  });

  it("MarkTitle_NamesNumberStarterAndOutcome", () => {
    const retried: TicketAttemptMark = {
      runId: "r3", status: "success", endedOnInfrastructure: false, handedBack: false,
      cause: { channel: "retry", actor: "Holger" },
    };
    const series: TicketAttemptMark = {
      runId: "r1", status: "failed", endedOnInfrastructure: false, handedBack: false,
      cause: { channel: "series", actor: "agent-smith" }, endKind: "preflight",
    };
    expect(markTitle(en, retried, 3)).toBe("Attempt 3 · restarted by Holger · delivered");
    expect(markTitle(en, series, 1)).toBe("Attempt 1 · started by the series · failed: a precondition did not hold");
    expect(markTitle(translator("de"), retried, 3)).toBe("Versuch 3 · neu gestartet von Holger · geliefert");
  });

  it("MarkTitle_InfrastructureEnd_SaysTheMachineStoppedIt", () => {
    const reaped: TicketAttemptMark = { runId: "r1", status: "cancelled", endedOnInfrastructure: true, handedBack: false, cause: { channel: "retry" } };
    expect(markTitle(en, reaped, 1)).toBe("Attempt 1 · restarted by someone · stopped by the infrastructure, not the work");
  });
});
