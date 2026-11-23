import { describe, expect, it } from "vitest";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { mergesNow, seriesNeededChain, seriesNodeKind, seriesOverviewCounts, waitingPullRequests } from "../seriesNodeKind";
import { overviewDetail, seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-f562: one colour per ticket, read from what the server sent, and every count the Overview shows from it.

describe("seriesNodeKind", () => {
  it("SeriesNodeKind_ReadsEachColourFromTheHoldTheMergeOrderAndNeededBy", () => {
    const detail = overviewDetail();
    const kinds = Object.fromEntries(detail.tickets.map((t) => [t.ticketId, seriesNodeKind(t, detail)]));

    expect(kinds).toEqual({ 48: "merged", 50: "now", 61: "question", 59: "failed", 51: "needed", 55: "needed", 56: "pr", 63: "running", 62: "waiting" });
  });

  it("SeriesNodeKind_AReadyStepWhoseHoldIsNotTheMerge_IsNotNow", () => {
    const detail = overviewDetail();
    detail.tickets[1] = { ...detail.tickets[1], hold: hold("ci_running") };

    expect(seriesNodeKind(detail.tickets[1], detail)).toBe("needed");
    expect(mergesNow(detail, detail.mergeOrder[0])).toBe(false);
  });

  it("SeriesNodeKind_RedCiOrAnAdminHold_IsFailed_AnAskedQuestion_IsQuestion", () => {
    const red = seriesTicket({ hold: hold("ci_failed") });
    const admin = seriesTicket({ hold: hold("image_untrusted") });
    const asked = seriesTicket({ hold: hold("question") });
    const detail = seriesDetail({ tickets: [red, admin, asked] });

    expect([red, admin, asked].map((t) => seriesNodeKind(t, detail))).toEqual(["failed", "failed", "question"]);
  });
});

describe("seriesOverviewCounts", () => {
  it("SeriesOverviewCounts_ChipsListAndBar_CountTheSameTickets", () => {
    const counts = seriesOverviewCounts(overviewDetail());

    expect(counts.kinds).toEqual({ now: 1, needed: 2, question: 1, failed: 1, running: 1, pr: 1, waiting: 1, merged: 1 });
    expect(counts.neededFor).toEqual(["#62"]);
    expect(counts.yours).toBe(3);
    expect(counts.quiet).toBe(3);
    expect(counts.yours + counts.kinds.needed + counts.quiet + counts.kinds.merged).toBe(overviewDetail().tickets.length);
  });

  it("SeriesOverviewCounts_WithoutMergeRight_TheMergeNowIsNotYours", () => {
    expect(seriesOverviewCounts(overviewDetail(), false).yours).toBe(2);
  });
});

describe("seriesNeededChain", () => {
  it("SeriesNeededChain_IsEveryStepOthersStartOn", () => {
    expect([...seriesNeededChain(overviewDetail())].sort()).toEqual(["50", "51", "55"]);
  });
});

describe("waitingPullRequests", () => {
  it("WaitingPullRequests_AreTheOpenAndUnreadOnes", () => {
    const prs = overviewDetail().tickets[0].pullRequests.concat(overviewDetail().tickets[1].pullRequests,
      [{ ...overviewDetail().tickets[1].pullRequests[0], number: "1", state: "unread" }]);

    expect(waitingPullRequests(prs).map((pr) => pr.number)).toEqual(["9937", "1"]);
  });
});
