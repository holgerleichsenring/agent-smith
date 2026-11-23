import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { TicketHoldCase } from "@/types/tickets";
import { hold, list, ticket } from "../../__tests__/ticketFixtures";
import { attempt } from "../../detail/__tests__/detailFixtures";
import {
  countsFor, holdActLabel, holdAsk, holdRowAct, holdSentence, holdTag, isPersons, isYourMove, namesOf, sinceClock, toldOf,
  waitClause, yourMoveCount,
} from "../holdWords";

// 2026-11-23-31dcg: a hold's words, the same on every surface, in both languages.

const CASES: TicketHoldCase[] = [
  "image_untrusted", "bootstrap_missing", "preflight", "question", "handback", "readiness_unknown", "infrastructure", "interrupted",
  "criteria_unmet", "failed", "shortfall", "text_edited", "merge_next", "starts_on_merge", "working", "model_wait", "queued", "resuming",
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

  // 2026-11-23-31dci: a wait on nobody says since when and that nothing is to be done, in both languages.
  it("holdSentence_EachWait_SaysSinceWhen_AndNothingForYouToDo", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    const since = new Date(2026, 10, 23, 0, 12).toISOString();
    const en = translator("en");
    const de = translator("de");
    const of = (c: TicketHoldCase) => hold(c, { since, attempt: 2, names: ["#7"] });
    expect(holdSentence(of("model_wait"), en, now)).toBe("Waiting for the model since 00:12. Nothing for you to do.");
    expect(holdSentence(of("queued"), en, now)).toBe("Waiting for sandbox capacity since 00:12. Nothing for you to do.");
    expect(holdSentence(of("resuming"), en, now))
      .toBe("Attempt 2 has been waiting for sandbox capacity to resume since 00:12. Nothing for you to do.");
    expect(holdSentence(of("restarting"), en, now)).toBe("Restarts after the server stopped at 00:12. Nothing for you to do.");
    expect(holdSentence(of("prerequisite"), en, now)).toBe("Starts when #7 finishes, waiting since 00:12. Nothing for you to do.");
    expect(holdSentence(of("model_wait"), de, now)).toBe("Wartet seit 00:12 auf das Modell. Für dich gibt es nichts zu tun.");
    expect(holdSentence(of("queued"), de, now)).toBe("Wartet seit 00:12 auf Sandbox-Kapazität. Für dich gibt es nichts zu tun.");
    expect(holdSentence(hold("model_wait", { attempt: 2 }), en)).toBe("Attempt 2 is waiting for the model. Nothing for you to do.");
    expect(waitClause(hold("working", { since }), en, now)).toBeNull();
  });

  it("holdSentence_AnEscalatedWait_NamesTheMinutesAndTheKey_AndThatTheAdminWasTold", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    const since = new Date(2026, 10, 23, 0, 12).toISOString();
    const escalated = hold("model_wait", { addressee: "admin", since, escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } });
    expect(holdSentence(escalated, translator("en"), now))
      .toBe("Waiting for the model since 00:12, longer than 60 minutes (holds.escalate_after_minutes). Reported to the admin.");
    expect(holdSentence(escalated, translator("de"), now))
      .toBe("Wartet seit 00:12 auf das Modell, länger als 60 Minuten (holds.escalate_after_minutes). An den Admin gemeldet.");
  });

  it("sinceClock_TodayIsATime_AnOlderDayCarriesItsDate", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    expect(sinceClock(new Date(2026, 10, 23, 0, 12).toISOString(), now)).toBe("00:12");
    expect(sinceClock(new Date(2026, 10, 21, 9, 5).toISOString(), now)).toBe("21 Nov 09:05");
    expect(sinceClock("not a date", now)).toBe("not a date");
  });
});
