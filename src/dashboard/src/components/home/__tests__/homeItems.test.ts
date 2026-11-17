import { describe, expect, it } from "vitest";
import { ticket } from "@/components/tickets/__tests__/ticketFixtures";
import { moveAct, moveAsk, recentTickets, stateWords, waitingSentence, yourMoves } from "../homeItems";

// 2026-11-17-d6fba: what the home page lists under its composer.

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();
const waiting = (id: string, minutes: number) =>
  ticket({ ticketId: id, bucket: "needs_you", kind: "question", since: ago(minutes) });

describe("homeItems", () => {
  it("yourMoves_FourWaiting_ShowsTheThreeWaitingLongestOldestFirst", () => {
    const moves = yourMoves([waiting("a", 5), ticket({ ticketId: "w" }), waiting("b", 50), waiting("c", 500), waiting("d", 1)]);
    expect(moves.map((t) => t.ticketId)).toEqual(["c", "b", "a"]);
  });

  it("recentTickets_ExcludesWaitingTickets_NewestFirst", () => {
    const recent = recentTickets([ticket({ ticketId: "old", since: ago(90) }), waiting("a", 1), ticket({ ticketId: "new", since: ago(2) })]);
    expect(recent.map((t) => t.ticketId)).toEqual(["new", "old"]);
  });

  it("moveAsk_QuestionAndHandback_SayWhatIsAsked", () => {
    const asked = ticket({ bucket: "needs_you", kind: "question", pendingQuestion: { runId: "r", question: {
      questionId: "q", type: "Freeform", text: "Keep the outbox?\nmore", context: null, choices: [], defaultAnswer: null,
      askedAt: ago(1), answerDeadlineAt: null } } as never });
    expect(moveAsk(asked)).toBe("Keep the outbox?");
    expect(moveAct(asked)).toBe("Answer");
    const back = ticket({ bucket: "needs_you", kind: "handback", handback: { text: "Which tenant?", at: ago(3) } });
    expect(moveAsk(back)).toBe("Which tenant?");
    expect(moveAct(back)).toBe("Reply");
  });

  it("waitingSentence_CountsInWords", () => {
    expect(waitingSentence(0)).toBe("Nothing waits on you.");
    expect(waitingSentence(1)).toBe("One thing waits on you.");
    expect(waitingSentence(4)).toBe("4 things wait on you.");
  });

  it("stateWords_NamesTheTicketListSection", () => {
    expect(stateWords(ticket({ bucket: "waiting_on_review" }))).toBe("In review");
    expect(stateWords(ticket({ bucket: "working" }))).toBe("Working");
  });
});
