import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { seriesTicketState } from "../seriesTicketState";
import { seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcd: never a raw status word. 2026-11-25-9992a: the Tickets section renders the server's one kind in the
// viewer's language — the same kind the Overview colours and the master reads.

const t = translator("en");
const open = { repo: "server", number: "9937", url: "u", state: "open", ci: "red", ciFailure: null, ciLog: null, target: null };

describe("seriesTicketState", () => {
  it.each([
    ["running", "success", "working", "run"],
    ["question", "waiting_for_input", "asks a question", "need"],
    ["failed", "failed", "failed", "bad"],
    ["failed", "shortfall", "fell short", "bad"],
    ["waiting", "success", "done", "idle"],
    ["waiting", "something_new", "ended", "idle"],
  ] as const)("seriesTicketState_%s_%s", (kind, status, text, tone) => {
    expect(seriesTicketState(seriesTicket({ kind, status }), t)).toEqual({ text, tone });
  });

  it("seriesTicketState_TheKindWins_ARedCiIsFailed_NotAWaitingPullRequest", () => {
    expect(seriesTicketState(seriesTicket({ kind: "failed", status: "success", pullRequests: [open] }), t)).toEqual({ text: "failed", tone: "bad" });
    expect(seriesTicketState(seriesTicket({ kind: "pr", status: "success", pullRequests: [open] }), t).text).toBe("PR #9937 waiting");
  });

  // 2026-11-24-f9c5f
  it("seriesTicketState_LandedInASeriesWithABranch_IsMergedIntoTheSeriesBranch", () => {
    expect(seriesTicketState(seriesTicket({ landed: true }), t, "agent-smith/series/sr-1").text).toBe("merged into the series branch");
    expect(seriesTicketState(seriesTicket({ landed: true }), t).text).toBe("merged");
  });

  it("seriesTicketState_NoAttempt_IsNotStarted_AndGerman", () => {
    expect(seriesTicketState(seriesTicket(), t).text).toBe("not started");
    expect(seriesTicketState(seriesTicket({ landed: true }), translator("de")).text).toBe("gemergt");
  });
});
