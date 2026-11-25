import { describe, expect, it } from "vitest";
import { seriesNeededChain, waitingPullRequests } from "../seriesNodeKind";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-f562: the graph's needed-merge chain and the pull requests that wait. 2026-11-25-9992a: the colour is the
// server's ticket.kind (SeriesTicketState, proven there).

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
