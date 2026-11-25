import { describe, expect, it } from "vitest";
import type { SeriesRunPullRequest } from "@/types/seriesRuns";
import { seriesTicketBadges } from "../seriesCiState";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-0219: the host's word on a ticket's waiting pull requests — Conflict, Build not started, CI running, CI red.

const open = (over: Partial<SeriesRunPullRequest>): SeriesRunPullRequest => ({
  repo: "server", number: "9937", url: "https://git.test/server/pull/9937", state: "open", ci: null, ciFailure: null,
  ciLog: null, target: null, ...over,
});

describe("seriesCiState", () => {
  // 2026-11-25-9992a: the CI badge is the ticket's one CI from the server, never an aggregate of the rows in the browser.
  it("SeriesTicketBadges_TheTicketsOneCi_NoDraftMark", () => {
    const ticket = { ...overviewDetail().tickets[1], pullRequests: [open({ isDraft: true, ci: "not_started" }), open({ repo: "client", ci: "pending" })] };
    expect(seriesTicketBadges({ ...ticket, ci: "not_started" })).toEqual(["ciNotStarted"]);
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "pending" })], ci: "red" })).toEqual(["ciRed"]);
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "pending" })], ci: "pending" })).toEqual(["ciRunning"]);
    expect(seriesTicketBadges({ ...ticket, pullRequests: [open({ ci: "red" })], ci: "green" })).toEqual([]);
  });

  // 2026-11-24-6811a: the host's conflict comes before the CI — nothing merges it, green or not.
  it("SeriesTicketBadges_AConflictComesBeforeTheCi", () => {
    const ticket = { ...overviewDetail().tickets[1], pullRequests: [open({ hasConflicts: true, ci: "pending" })], ci: "pending" };
    expect(seriesTicketBadges(ticket)).toEqual(["conflict", "ciRunning"]);
  });
});
