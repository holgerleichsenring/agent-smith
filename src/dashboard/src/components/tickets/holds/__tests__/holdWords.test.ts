import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { TicketHoldCase } from "@/types/tickets";
import { hold, list, ticket } from "../../__tests__/ticketFixtures";
import { attempt } from "../../detail/__tests__/detailFixtures";
import {
  countsFor, durationOf, holdActLabel, holdAsk, holdRowAct, holdSentence, holdTag, isPersons, isYourMove, mergeTail, namesOf, sinceClock, toldOf,
  waitClause, yourMoveCount,
} from "../holdWords";
import { holdPullRequestHref, holdSeriesHref, isAsked } from "../holdRoutes";

// 2026-11-23-31dcg: a hold's words, the same on every surface, in both languages.

const CASES: TicketHoldCase[] = [
  "image_untrusted", "bootstrap_missing", "preflight", "question", "question_plan", "question_environment", "handback", "readiness_unknown", "infrastructure", "interrupted",
  "criteria_unmet", "failed", "shortfall", "text_edited", "merge_next", "starts_on_merge", "working", "model_wait", "queued", "resuming",
  "restarting", "merge_after", "retarget", "prerequisite", "merge_ready", "ci_failed", "ci_running",
  "ci_not_started", "pr_conflict", "refreshing", "refresh_refused",
];

describe("holdWords", () => {
  it("holdSentence_EveryCase_HasASentenceInBothLanguages_NamingTheAttemptAndTheTickets", () => {
    for (const c of CASES) {
      for (const language of ["en", "de"] as const) {
        const said = holdSentence(translator(language), hold(c, { attempt: 3, names: ["#7"] }));
        expect(said, `${c} ${language}`).not.toMatch(/\{|hold\.sentence/);
        expect(said.length, `${c} ${language}`).toBeGreaterThan(10);
      }
    }
    expect(holdSentence(translator("en"), hold("failed", { attempt: 3 }))).toBe("Attempt 3 failed.");
    expect(holdSentence(translator("en"), hold("merge_after", { names: ["SCRUM-1", "SCRUM-2"] }))).toBe("Merges after SCRUM-1 and SCRUM-2. Nothing for you to do yet.");
    expect(holdSentence(translator("de"), hold("starts_on_merge", { addressee: "nobody", names: ["#1"] })))
      .toBe("Wartet auf einen Merge: #1. Für dich gibt es nichts zu tun.");
  });

  // 2026-11-23-849e: the series' one merge says what merging unblocks; a ticket that starts on merges
  // says how many it waits for and which is next; a CI that ran past the minutes says how long and what to check.
  // 2026-11-23-0219: a required build that never started, past the minutes, names how long and the key.
  it("holdSentence_ABuildNotStartedPastTheMinutes_IsReportedWithTheKey", () => {
    const since = new Date(Date.now() - 3 * 3600_000).toISOString();
    const said = holdSentence(translator("en"), hold("ci_not_started", { addressee: "admin", since, escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } }));
    expect(said).toContain("The required build has not started for");
    expect(said).toContain("holds.escalate_after_minutes");
  });

  it("holdSentence_TheSeriesMerge_TheMergesAStartWaitsFor_AndAHungCi", () => {
    // 2026-11-25-9992h: merging never approves, so the sentence no longer says it does.
    const merge = hold("merge_next", { next: "#19649", names: ["#19662", "#19665"] });
    expect(holdSentence(translator("en"), merge)).toBe("Its pull request is next in the series' merge order. #19649 is next after it. Needed so "
      + "#19662 and #19665 can start.");
    expect(mergeTail(translator("en"), hold("merge_next", { next: "#2" }))).toBe("#2 is next after it.");
    // 2026-11-23-f562: the ticket page says the needed-merge wording, not "erst nach weiteren Merges".
    expect(mergeTail(translator("de"), hold("merge_next", { names: ["#9"] }))).toBe("Nötig, damit #9 starten kann.");
    expect(mergeTail(translator("de"), hold("merge_next", { next: "#2", names: ["#9"] }))).toBe("Danach ist #2 dran. Nötig, damit #9 starten kann.");
    expect(mergeTail(translator("en"), hold("merge_next"))).toBeNull();
    const waits = hold("starts_on_merge", { addressee: "nobody", names: ["#19653"], merges: 5, next: "#19648" });
    expect(holdSentence(translator("en"), waits)).toBe("Waits for 5 merges, next #19648. Nothing for you to do.");
    expect(holdSentence(translator("de"), waits)).toBe("Wartet auf 5 Merges, als Nächstes #19648. Für dich gibt es nichts zu tun.");
    const now = new Date("2026-11-23T12:00:00Z");
    const hung = hold("ci_running", { addressee: "admin", since: "2026-11-23T07:00:00Z", escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } });
    expect(holdSentence(translator("en"), hung, now))
      .toBe("CI has been running for 5 h: check the pipeline. Longer than 60 minutes (holds.escalate_after_minutes), reported to the admin.");
    expect([durationOf("2026-11-23T11:15:00Z", now), durationOf("2026-11-20T12:00:00Z", now)]).toEqual(["45 min", "3 d"]);
  });

  it("holdSentence_AStop_QuotesWhatItRecorded_FromTheHoldAlone", () => {
    const detail = { subject: "Image node:22", repos: [], control: "Trusted image registries", setTo: "ghcr.io/", settingsPath: "/x" };
    expect(holdSentence(translator("en"), hold("image_untrusted", { detail }))).toBe("Image node:22 is not trusted: set Trusted image registries to ghcr.io/.");
    expect(holdSentence(translator("en"), hold("failed", { attempt: 2, detail: { ...detail, subject: "The agent" } })))
      .toBe("Attempt 2 failed: The agent: set Trusted image registries to ghcr.io/.");
    expect(holdSentence(translator("de"), hold("image_untrusted", { detail })))
      .toBe("Image node:22 ist nicht vertraut: setze Trusted image registries auf ghcr.io/.");
  });

  it("namesOf_AndToldOf", () => {
    expect([namesOf(translator("en"), []), namesOf(translator("en"), ["#1"]), namesOf(translator("en"), ["#1", "#2", "#3"])]).toEqual(["", "#1", "#1, #2 and #3"]);
    expect(toldOf(translator("en"), attempt({ endKind: "bootstrap-missing", endDetail: { subject: "s", repos: ["api"], control: null, setTo: null, settingsPath: null } })))
      .toBe("api has no agent-smith bootstrap.");
    expect(toldOf(translator("en"), attempt())).toBeNull();
  });

  it("holdActLabel_AndRowAct_NameTheAct", () => {
    expect(holdActLabel(translator("en"), hold("image_untrusted", { act: { kind: "settings", target: "/x" } }))).toBe("Trust the registry in Settings");
    expect(holdActLabel(translator("en"), hold("preflight", { act: { kind: "settings", target: "/x" } }))).toBe("Open the setting");
    expect(holdActLabel(translator("en"), hold("merge_next", { act: { kind: "merge", target: "sr-1" } }))).toBe("Merge");
    expect(holdActLabel(translator("en"), hold("working"))).toBeNull();
    expect(holdRowAct(translator("en"), hold("text_edited", { act: { kind: "conversation", target: null } }))).toBe("Discuss in the conversation");
    expect(holdRowAct(translator("en"), hold("working"))).toBe("Open the ticket");
    expect(holdTag(translator("en"), hold("text_edited"))).toBe("plan gap");
  });

  // 2026-11-23-31dch: a plan question's act is the series' conversation, in both languages.
  it("aPlanQuestion_TalksAboutThePlanInTheSeriesConversation", () => {
    const plan = hold("question_plan", { act: { kind: "conversation", target: "sr-1" } });
    expect([holdActLabel(translator("en"), plan), holdRowAct(translator("en"), plan), holdRowAct(translator("de"), plan)])
      .toEqual(["Talk about the plan", "Talk about the plan", "Über den Plan sprechen"]);
    const path = (id: string) => `/series/${id}`;
    expect(holdSeriesHref(plan, path)).toBe("/series/sr-1?section=talk");
    expect(holdSeriesHref(hold("starts_on_merge", { act: { kind: "series", target: "sr-1" } }), path)).toBe("/series/sr-1");
    expect(holdActLabel(translator("de"), hold("starts_on_merge", { act: { kind: "series", target: "sr-1" } }))).toBe("Serie öffnen");
    expect(holdSeriesHref(hold("text_edited", { act: { kind: "conversation", target: null } }), path)).toBeNull();
    expect([isAsked(plan), isAsked(hold("question_environment")), isAsked(hold("question")), isAsked(hold("handback"))])
      .toEqual([true, true, true, false]);
    expect(holdTag(translator("en"), hold("question_environment"))).toBe("needs an admin");
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
    expect(holdAsk(translator("en"), asked)).toBe("Keep it?");
    expect(holdAsk(translator("en"), { ...asked, hold: hold("question_plan") })).toBe("Keep it?");
    expect(holdAsk(translator("en"), ticket({ hold: hold("handback"), handback: { text: "Which tenant?", at: "" } }))).toBe("Which tenant?");
    expect(holdAsk(translator("en"), ticket({ hold: hold("merge_next") }))).toBe("Its pull request is next in the series' merge order.");
  });

  // 2026-11-23-31dci: a wait on nobody says since when and that nothing is to be done, in both languages.
  it("holdSentence_EachWait_SaysSinceWhen_AndNothingForYouToDo", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    const since = new Date(2026, 10, 23, 0, 12).toISOString();
    const en = translator("en");
    const de = translator("de");
    const of = (c: TicketHoldCase) => hold(c, { since, attempt: 2, names: ["#7"] });
    expect(holdSentence(en, of("model_wait"), now)).toBe("Waiting for the model since 00:12. Nothing for you to do.");
    expect(holdSentence(en, of("queued"), now)).toBe("Waiting for sandbox capacity since 00:12. Nothing for you to do.");
    expect(holdSentence(en, of("resuming"), now))
      .toBe("Attempt 2 has been waiting for sandbox capacity to resume since 00:12. Nothing for you to do.");
    expect(holdSentence(en, of("restarting"), now)).toBe("Restarts after the server stopped at 00:12. Nothing for you to do.");
    expect(holdSentence(en, of("prerequisite"), now)).toBe("Starts when #7 finishes, waiting since 00:12. Nothing for you to do.");
    expect(holdSentence(de, of("model_wait"), now)).toBe("Wartet seit 00:12 auf das Modell. Für dich gibt es nichts zu tun.");
    expect(holdSentence(de, of("queued"), now)).toBe("Wartet seit 00:12 auf Sandbox-Kapazität. Für dich gibt es nichts zu tun.");
    expect(holdSentence(en, hold("model_wait", { attempt: 2 }))).toBe("Attempt 2 is waiting for the model. Nothing for you to do.");
    expect(waitClause(en, hold("working", { since }), now)).toBeNull();
  });

  it("holdSentence_AnEscalatedWait_NamesTheMinutesAndTheKey_AndThatTheAdminWasTold", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    const since = new Date(2026, 10, 23, 0, 12).toISOString();
    const escalated = hold("model_wait", { addressee: "admin", since, escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } });
    expect(holdSentence(translator("en"), escalated, now))
      .toBe("Waiting for the model since 00:12, longer than 60 minutes (holds.escalate_after_minutes). Reported to the admin.");
    expect(holdSentence(translator("de"), escalated, now))
      .toBe("Wartet seit 00:12 auf das Modell, länger als 60 Minuten (holds.escalate_after_minutes). An den Admin gemeldet.");
  });

  it("sinceClock_TodayIsATime_AnOlderDayCarriesItsDate", () => {
    const now = new Date(2026, 10, 23, 9, 0);
    expect(sinceClock(new Date(2026, 10, 23, 0, 12).toISOString(), "en-GB", now)).toBe("00:12");
    expect(sinceClock(new Date(2026, 10, 21, 9, 5).toISOString(), "en-GB", now)).toBe("21 Nov 09:05");
    expect(sinceClock("not a date", "en-GB", now)).toBe("not a date");
  });
  // 2026-11-23-d60d: an open pull request's holds — it merges now, its CI failed on named checks, its CI still runs.
  it("holdSentence_AnOpenPullRequest_MergesNow_NamesItsFailedChecks_SaysSinceWhenCiRuns", () => {
    const de = translator("de");
    const open = { kind: "pull_request" as const, target: "https://example.test/pr/1" };
    expect(holdSentence(translator("en"), hold("merge_ready", { act: open }))).toBe("Its pull request can be merged now.");
    expect(holdSentence(de, hold("merge_ready", { act: open }))).toBe("Sein Pull Request kann jetzt gemergt werden.");
    expect(holdSentence(translator("en"), hold("ci_failed", { names: ["build", "lint"] }))).toBe("CI failed: build and lint.");
    expect(holdSentence(de, hold("ci_failed", { names: ["build"] }))).toBe("Die CI ist fehlgeschlagen: build.");
    expect(holdSentence(translator("en"), hold("ci_failed"))).toBe("CI failed on its pull request.");
    const now = new Date(2026, 10, 23, 9, 30);
    const since = new Date(2026, 10, 23, 9, 12).toISOString();
    expect(holdSentence(translator("en"), hold("ci_running", { since }), now)).toBe("CI has been running since 09:12. Nothing for you to do.");
    expect(holdSentence(de, hold("ci_running", { since }), now)).toBe("Die CI läuft seit 09:12. Für dich gibt es nichts zu tun.");
    expect(holdSentence(translator("en"), hold("ci_running"))).toBe("CI is running on its pull request. Nothing for you to do.");
    expect(holdActLabel(de, hold("merge_ready", { act: open }))).toBe("Pull Request öffnen");
    expect(holdActLabel(translator("en"), hold("merge_ready", { act: { kind: "pull_request", target: null } }))).toBeNull();
    expect(holdRowAct(translator("en"), hold("ci_failed", { act: open }))).toBe("Open the pull request");
    expect(holdPullRequestHref(hold("merge_ready", { act: open }))).toBe("https://example.test/pr/1");
    expect(holdPullRequestHref(hold("merge_next", { act: { kind: "merge", target: "sr-1" } }))).toBeNull();
    expect(isPersons(hold("merge_ready"))).toBe(true);
    expect(isPersons(hold("ci_running"))).toBe(false);
  });
  // 2026-11-24-6811d: a refresh's wait says since when; its refusal quotes the reason it recorded.
  it("holdSentence_ARefresh_WaitsSinceItsStart_ItsRefusalQuotesTheReason", () => {
    const en = translator("en");
    expect(holdSentence(en, hold("refreshing", { since: "2026-11-24T09:00:00Z" }), new Date("2026-11-24T09:30:00Z")))
      .toMatch(/^Being brought up to date with its base since .+\. Nothing for you to do\.$/);
    expect(holdSentence(en, hold("refresh_refused", { detail: { subject: "Merging origin/main conflicts in Program.cs.", repos: [] } as never })))
      .toBe("Bringing its pull request up to date did not work, and the branch is unchanged: Merging origin/main conflicts in Program.cs.");
  });
});
