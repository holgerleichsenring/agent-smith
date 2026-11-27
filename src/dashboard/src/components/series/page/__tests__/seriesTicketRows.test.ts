import { describe, expect, it } from "vitest";
import { SERIES_NODE_KINDS } from "../seriesNodeKind";
import { seriesBucketCounts, seriesTicketBucket, seriesTicketRows, seriesWaitsOn } from "../seriesTicketRows";
import { overviewDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-27-ecf2a: the rail's buckets, filter, order and the tickets a waiting one waits on.

describe("seriesTicketRows", () => {
  it("seriesTicketBucket puts each kind in merged, moving, needs you or waiting", () => {
    const buckets = Object.fromEntries(SERIES_NODE_KINDS.map((kind) => [kind, seriesTicketBucket(kind)]));

    expect(buckets).toEqual({
      now: "moving", needed: "moving", question: "you", approval: "you", draft: "you", failed: "you",
      running: "moving", pr: "moving", waiting: "waiting", merged: "merged",
    });
  });

  it("seriesBucketCounts counts every ticket once", () => {
    const counts = seriesBucketCounts(overviewDetail().tickets);

    expect(counts).toEqual({ merged: 1, moving: 5, you: 2, waiting: 1 });
  });

  it("the Merged filter lists only merged tickets, Open only the rest, rows deepest first", () => {
    const tickets = overviewDetail().tickets;

    expect(seriesTicketRows(tickets, "merged").map((t) => t.ticketId)).toEqual(["48"]);
    expect(seriesTicketRows(tickets, "open").map((t) => t.ticketId)).not.toContain("48");
    expect(seriesTicketRows(tickets, "all").map((t) => t.ticketId)).toEqual(["62", "63", "56", "55", "51", "59", "61", "50", "48"]);
  });

  it("a waiting ticket's short state names the unmerged tickets it waits on", () => {
    const tickets = [
      seriesTicket({ ticketId: "1", key: "#1", kind: "merged", landed: true }),
      seriesTicket({ ticketId: "2", key: "#2", kind: "pr" }),
      seriesTicket({ ticketId: "3", key: "#3", kind: "waiting", requires: ["1", "2", "outside"] }),
    ];

    expect(seriesWaitsOn(tickets[2], tickets)).toEqual(["#2"]);
    expect(seriesWaitsOn({ ...tickets[1], requires: ["1"] }, tickets)).toEqual([]);
  });
});
