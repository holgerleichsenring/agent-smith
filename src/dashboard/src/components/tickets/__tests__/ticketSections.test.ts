import { describe, expect, it } from "vitest";
import { SERVER_BUCKET, TICKET_BUCKETS } from "@/types/tickets";
import { TICKET_SECTIONS, totalTickets } from "../ticketSections";
import { counts } from "./ticketFixtures";

// 2026-10-15-114fd: PR closed and Failed split out of Ended, Closed drawn last — each a section with its own count.

describe("ticketSections", () => {
  it("ticketSections_ClosedFailedPrClosed_AreListedInOrderAndCounted", () => {
    expect(TICKET_BUCKETS.slice(TICKET_BUCKETS.indexOf("stuck"))).toEqual(
      ["stuck", "blocked", "filed", "pr-closed", "failed", "ended", "done", "closed"]);
    expect([SERVER_BUCKET["pr-closed"], SERVER_BUCKET.failed, SERVER_BUCKET.closed, SERVER_BUCKET.ended])
      .toEqual(["pr_closed", "failed", "closed", "idle"]);

    const c = counts({ needsYou: 1, working: 2, waitingOnReview: 3, stuck: 4, blocked: 5, filed: 6, prClosed: 7, failed: 8, idle: 9, done: 10, closed: 11 });
    expect(TICKET_SECTIONS["pr-closed"]).toMatchObject({ title: "PR closed", sub: "every pull request was closed without a merge", tone: "red" });
    expect(TICKET_SECTIONS.failed).toMatchObject({ title: "Failed", sub: "the last attempt failed", tone: "red" });
    expect(TICKET_SECTIONS.closed).toMatchObject({ title: "Closed", sub: "a person stopped work on it; later activity brings it back" });
    expect(TICKET_SECTIONS.closed.tone).toBeUndefined();
    expect(TICKET_SECTIONS.ended.sub).toBe("cancelled, or finished without a pull request");
    expect([TICKET_SECTIONS["pr-closed"], TICKET_SECTIONS.failed, TICKET_SECTIONS.closed].map((s) => s.count(c))).toEqual([7, 8, 11]);
    expect(totalTickets(c)).toBe(66);
    expect(totalTickets(counts({ idle: 2 }))).toBe(2);
  });
});
