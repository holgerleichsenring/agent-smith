import { describe, expect, it } from "vitest";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { translator } from "@/lib/i18n/translate";
import type { SeriesRunDetail, SeriesRunPullRequest, SeriesRunTicket } from "@/types/seriesRuns";
import { mergesWhenGreen, seriesCiWait, seriesMergeSaid, seriesTicketBadges } from "../seriesCiState";
import { seriesNodeKind } from "../seriesNodeKind";
import { seriesNodeSentence, seriesTicketActs } from "../seriesTicketActs";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-0219: the host's word on a ticket's waiting pull requests — Draft, Build not started, CI running, CI red — and a
// ready merge whose CI waits: Merge when CI is green, armed under respect_branch_protection, else naming the key.

const t = translator("en");
const open = (over: Partial<SeriesRunPullRequest>): SeriesRunPullRequest => ({
  repo: "server", number: "9937", url: "https://git.test/server/pull/9937", state: "open", ci: null, ciFailure: null,
  ciLog: null, target: null, ...over,
});

// #50 is the ready step; with its CI waiting its hold is no longer the series' merge.
function ready(pr: Partial<SeriesRunPullRequest>, arms: boolean): { detail: SeriesRunDetail; ticket: SeriesRunTicket } {
  const detail = { ...overviewDetail(), mergeArms: arms };
  const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "50");
  detail.tickets[at] = { ...detail.tickets[at], pullRequests: [open(pr)], hold: hold(pr.ci === "not_started" ? "ci_not_started" : "ci_running") };
  return { detail, ticket: detail.tickets[at] };
}

describe("seriesCiState", () => {
  it("SeriesTicketBadges_DraftAndTheCiThatHoldsTheMerge", () => {
    const ticket = { ...overviewDetail().tickets[1], pullRequests: [open({ isDraft: true, ci: "not_started" }), open({ repo: "client", ci: "pending" })] };
    expect(seriesTicketBadges(ticket)).toEqual(["draft", "ciNotStarted"]);
    expect(seriesCiWait(ticket)).toBe("not_started");
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "red" }), open({ ci: "pending" })] })).toEqual(["ciRed"]);
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "pending" })] })).toEqual(["ciRunning"]);
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "green" })] })).toEqual([]);
  });

  // 2026-11-24-6811a: the host's conflict comes before the CI — nothing merges it, green or not.
  it("SeriesTicketBadges_AConflictComesBeforeTheCi", () => {
    const ticket = { ...overviewDetail().tickets[1], pullRequests: [open({ hasConflicts: true, ci: "pending" })] };
    expect(seriesTicketBadges(ticket)).toEqual(["conflict", "ciRunning"]);
  });

  it("MergesWhenGreen_AReadyStepWhoseCiRunsOrHasNotStarted_NeverRed", () => {
    expect(mergesWhenGreen(ready({ ci: "pending" }, true).detail, ready({ ci: "pending" }, true).ticket)).toBe(true);
    const notStarted = ready({ ci: "not_started" }, true);
    expect(mergesWhenGreen(notStarted.detail, notStarted.ticket)).toBe(true);
    const red = ready({ ci: "red" }, true);
    expect(mergesWhenGreen(red.detail, red.ticket)).toBe(false);
    const later = overviewDetail();
    const after = { ...later.tickets.find((ticket) => ticket.ticketId === "51")!, pullRequests: [open({ ci: "pending" })] };
    expect(mergesWhenGreen(later, after)).toBe(false);
  });

  it("SeriesTicketActs_CiRunningUnderRespect_MergeWhenGreenEnabled", () => {
    const { detail, ticket } = ready({ ci: "pending" }, true);
    const merge = seriesTicketActs(ticket, detail, seriesNodeKind(ticket, detail), false, t)[0];
    expect([merge.id, merge.label, merge.disabledBy]).toEqual(["merge", "Merge when CI is green", undefined]);
  });

  it("SeriesTicketActs_BuildNotStartedWithoutRespect_DisabledNamingTheKey_TheSentenceSaysSo", () => {
    const { detail, ticket } = ready({ ci: "not_started" }, false);
    const kind = seriesNodeKind(ticket, detail);
    const merge = seriesTicketActs(ticket, detail, kind, false, t)[0];
    expect(merge.label).toBe("Merge when CI is green");
    expect(merge.disabledBy).toContain("merge.respect_branch_protection: true (Config → Project → Merge)");
    expect(seriesNodeSentence(ticket, detail, kind, t)).toContain("merge.respect_branch_protection");
  });

  it("SeriesMergeSaid_TheLastRefusalStaysInTheSentence", () => {
    const { detail, ticket } = ready({ ci: "pending", mergeSaid: { status: "completion_refused", reason: "cannot bypass branch policies", at: "2026-11-23T10:00:00Z" } }, true);
    expect(seriesMergeSaid(ticket)?.reason).toBe("cannot bypass branch policies");
    expect(seriesNodeSentence(ticket, detail, seriesNodeKind(ticket, detail), t))
      .toContain("The last merge was refused: cannot bypass branch policies");
  });
});
