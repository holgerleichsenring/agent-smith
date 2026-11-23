import { describe, expect, it } from "vitest";
import { hold, ticket } from "@/components/tickets/__tests__/ticketFixtures";
import { translator } from "@/lib/i18n/translate";
import { recentTickets, stateWords, waitingSentence, yourMoves } from "../homeItems";

// 2026-11-17-d6fba: what the home page lists under its composer. 2026-11-23-31dcg: Your move is the holds.

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();
const waiting = (id: string, minutes: number) =>
  ticket({ ticketId: id, bucket: "needs_you", kind: "question", since: ago(minutes), hold: hold("question") });

describe("homeItems", () => {
  it("yourMoves_FourWaiting_ShowsTheThreeWaitingLongestOldestFirst", () => {
    const moves = yourMoves([waiting("a", 5), ticket({ ticketId: "w" }), waiting("b", 50), waiting("c", 500), waiting("d", 1)]);
    expect(moves.map((t) => t.ticketId)).toEqual(["c", "b", "a"]);
  });

  it("yourMoves_AMergeOrADecisionIsYourMove_NobodysIsNot_AnAdminsOnlyForAnAdmin", () => {
    const tickets = [
      ticket({ ticketId: "m", since: ago(4), hold: hold("merge_next") }),
      ticket({ ticketId: "f", since: ago(3), hold: hold("failed") }),
      ticket({ ticketId: "n", since: ago(2), hold: hold("merge_after") }),
      ticket({ ticketId: "x", since: ago(1), hold: hold("preflight") }),
    ];
    expect(yourMoves(tickets).map((t) => t.ticketId)).toEqual(["m", "f"]);
    expect(yourMoves(tickets, true).map((t) => t.ticketId)).toEqual(["m", "f", "x"]);
    expect(recentTickets(tickets).map((t) => t.ticketId)).toEqual(["x", "n"]);
  });

  it("recentTickets_ExcludesWaitingTickets_NewestFirst", () => {
    const recent = recentTickets([ticket({ ticketId: "old", since: ago(90) }), waiting("a", 1), ticket({ ticketId: "new", since: ago(2) })]);
    expect(recent.map((t) => t.ticketId)).toEqual(["new", "old"]);
  });

  it("waitingSentence_CountsInWords", () => {
    expect(waitingSentence(translator("en"), 0)).toBe("Nothing waits on you.");
    expect(waitingSentence(translator("en"), 1)).toBe("One thing waits on you.");
    expect(waitingSentence(translator("en"), 4)).toBe("4 things wait on you.");
  });

  it("stateWords_NamesTheTicketListSection", () => {
    expect(stateWords(ticket({ bucket: "stuck" }))).toBe("Stuck");
    expect(stateWords(ticket({ bucket: "working" }))).toBe("Working");
  });
});
