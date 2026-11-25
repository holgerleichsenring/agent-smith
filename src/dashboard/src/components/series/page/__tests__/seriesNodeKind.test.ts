import { describe, expect, it } from "vitest";
import { seriesNeededChain, seriesOverviewCounts, waitingPullRequests } from "../seriesNodeKind";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-f562: one colour per ticket and every count the Overview shows from it. 2026-11-25-9992a: the colour is the
// server's ticket.kind (SeriesTicketState, proven there); the browser only counts it.

describe("seriesOverviewCounts kinds", () => {
  it("SeriesOverviewCounts_CountsTheServersKinds_NotAReadingOfTheHold", () => {
    const detail = overviewDetail();
    detail.tickets[1] = { ...detail.tickets[1], kind: "failed" }; // whatever its hold, the server's kind counts

    expect(seriesOverviewCounts(detail).kinds.failed).toBe(2);
    expect(seriesOverviewCounts(detail).kinds.now).toBe(0);
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
