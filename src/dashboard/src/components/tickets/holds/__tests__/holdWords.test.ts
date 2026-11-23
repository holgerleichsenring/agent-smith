import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { TicketHoldCase } from "@/types/tickets";
import { hold, list, ticket } from "../../__tests__/ticketFixtures";
import { attempt } from "../../detail/__tests__/detailFixtures";
import {
  countsFor, holdActLabel, holdAsk, holdRowAct, holdSentence, holdTag, isPersons, isYourMove, namesOf, toldOf, yourMoveCount,
} from "../holdWords";

// 2026-11-23-31dcg: a hold's words, the same on every surface, in both languages.

const CASES: TicketHoldCase[] = [
  "image_untrusted", "bootstrap_missing", "preflight", "question", "handback", "readiness_unknown", "infrastructure", "interrupted",
  "criteria_unmet", "failed", "shortfall", "text_edited", "merge_next", "starts_on_merge", "working", "queued", "resuming",
  "restarting", "merge_after", "retarget", "prerequisite",
];

describe("holdWords", () => {
  it("holdSentence_EveryCase_HasASentenceInBothLanguages_NamingTheAttemptAndTheTickets", () => {
    for (const c of CASES) {
      for (const language of ["en", "de"] as const) {
        const said = holdSentence(hold(c, { attempt: 3, names: ["#7"] }), translator(language));
        expect(said, `${c} ${language}`).not.toMatch(/\{|hold\.sentence/);
        expect(said.length, `${c} ${language}`).toBeGreaterThan(10);
      }
    }
    expect(holdSentence(hold("failed", { attempt: 3 }))).toBe("Attempt 3 failed.");
    expect(holdSentence(hold("merge_after", { names: ["SCRUM-1", "SCRUM-2"] }))).toBe("Merges after SCRUM-1 and SCRUM-2. Nothing for you to do yet.");
    expect(holdSentence(hold("starts_on_merge", { names: ["#1"] }), translator("de"))).toBe("Startet, sobald #1 gemergt ist.");
  });

  it("holdSentence_AStop_QuotesWhatItRecorded_FromTheHoldAlone", () => {
    const detail = { subject: "Image node:22", repos: [], control: "Trusted image registries", setTo: "ghcr.io/", settingsPath: "/x" };
    expect(holdSentence(hold("image_untrusted", { detail }))).toBe("Image node:22 is not trusted: set Trusted image registries to ghcr.io/.");
    expect(holdSentence(hold("failed", { attempt: 2, detail: { ...detail, subject: "The agent" } })))
      .toBe("Attempt 2 failed: The agent: set Trusted image registries to ghcr.io/.");
    expect(holdSentence(hold("image_untrusted", { detail }), translator("de")))
      .toBe("Image node:22 ist nicht vertraut: setze Trusted image registries auf ghcr.io/.");
  });

  it("namesOf_AndToldOf", () => {
    expect([namesOf([]), namesOf(["#1"]), namesOf(["#1", "#2", "#3"])]).toEqual(["", "#1", "#1, #2 and #3"]);
    expect(toldOf(attempt({ endKind: "bootstrap-missing", endDetail: { subject: "s", repos: ["api"], control: null, setTo: null, settingsPath: null } })))
      .toBe("api has no agent-smith bootstrap.");
    expect(toldOf(attempt())).toBeNull();
  });

  it("holdActLabel_AndRowAct_NameTheAct", () => {
    expect(holdActLabel(hold("image_untrusted", { act: { kind: "settings", target: "/x" } }))).toBe("Trust the registry in Settings");
    expect(holdActLabel(hold("preflight", { act: { kind: "settings", target: "/x" } }))).toBe("Open the setting");
    expect(holdActLabel(hold("merge_next", { act: { kind: "merge", target: "sr-1" } }))).toBe("Merge");
    expect(holdActLabel(hold("working"))).toBeNull();
    expect(holdRowAct(hold("text_edited", { act: { kind: "conversation", target: null } }))).toBe("Discuss in the conversation");
    expect(holdRowAct(hold("working"))).toBe("Open the ticket");
    expect(holdTag(hold("text_edited"))).toBe("plan gap");
  });

  it("isYourMove_APersonsHold_AndAnAdminsForAnAdmin", () => {
    expect([isPersons(hold("merge_next")), isPersons(hold("text_edited")), isPersons(hold("failed")), isPersons(hold("preflight")),
      isPersons(hold("working")), isPersons(null)]).toEqual([true, true, true, false, false, false]);
    const admin = ticket({ hold: hold("preflight") });
    expect([isYourMove(admin, false), isYourMove(admin, true)]).toEqual([false, true]);
  });

  it("yourMoveCount_AndCountsFor_AddTheAdminHoldsForAnAdmin", () => {
    const { counts } = list([], { needsYou: 2, adminHolds: 1, stuck: 1 });
    expect([yourMoveCount(counts, false), yourMoveCount(counts, true), yourMoveCount(null, true)]).toEqual([2, 3, undefined]);
    expect(countsFor(counts, true)).toMatchObject({ needsYou: 3, stuck: 0 });
    expect(countsFor(counts, false)).toBe(counts);
  });

  it("holdAsk_TheQuestionOrHandbackItself_ElseTheSentence", () => {
    const asked = ticket({ hold: hold("question"), pendingQuestion: { runId: "r", question: {
      questionId: "q", type: "FreeText", text: "Keep it?\nmore", context: null, choices: [], defaultAnswer: null, askedAt: "", answerDeadlineAt: null } } as never });
    expect(holdAsk(asked)).toBe("Keep it?");
    expect(holdAsk(ticket({ hold: hold("handback"), handback: { text: "Which tenant?", at: "" } }))).toBe("Which tenant?");
    expect(holdAsk(ticket({ hold: hold("merge_next") }))).toBe("Its pull request is next in the series' merge order.");
  });
});
