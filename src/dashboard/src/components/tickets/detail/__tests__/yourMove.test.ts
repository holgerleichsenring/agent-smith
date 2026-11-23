import { describe, expect, it } from "vitest";
import type { TicketHold, TicketServerBucket } from "@/types/tickets";
import { ticketState } from "../ticketState";
import { actLabel, yourMove } from "../yourMove";
import { attempt, detail } from "./detailFixtures";
import { hold, pr } from "../../__tests__/ticketFixtures";

// 2026-11-16-6877a: the Your move box reads ticketState's view; each state gets its colour, one title and its act.
// 2026-11-23-31dcg: a held ticket's title, colour and act are its hold's.

const QUESTION = {
  questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null,
  askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: "2026-09-05T14:10:00Z",
};

function moveOf(bucket: TicketServerBucket, head = attempt(), over: Partial<ReturnType<typeof detail>["summary"]> = {}) {
  const d = detail([head]);
  d.summary = { ...d.summary, bucket, ...over };
  const view = ticketState({ detail: d, liveEvents: [], series: null })!;
  return yourMove(view, bucket, view.attempt?.endDetail);
}

const failedHold = (h: TicketHold) => ({ hold: h });

describe("yourMove", () => {
  // 2026-11-22-7103a: a question or a hand-back is a person's move — blue.
  it("yourMove_WaitingForAnAnswer_IsBlue_AndAsksForTheAnswer", () => {
    const head = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: QUESTION });
    const move = moveOf("needs_you", head, { kind: "question", pendingQuestion: { runId: "r-1", question: QUESTION },
      hold: hold("question", { act: { kind: "answer", target: null } }) });
    expect(move).toMatchObject({ tone: "person", title: "Your move: answer the question", detail: "Keep the outbox?", why: false, next: "Answer the question" });
    expect(move.act).toMatchObject({ kind: "tab", tab: "conversation" });
  });

  // 2026-11-23-31dch: a plan question is talked through in the series' conversation; an environment one is the admin's.
  it("yourMove_APlanQuestion_OpensTheSeriesConversation_AnEnvironmentOne_IsReportedToTheAdmin", () => {
    const head = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: QUESTION });
    const pending = { kind: "question" as const, pendingQuestion: { runId: "r-1", question: QUESTION } };
    const plan = moveOf("needs_you", head, { ...pending, hold: hold("question_plan", { act: { kind: "conversation", target: "sr-1" } }) });
    expect(plan).toMatchObject({ tone: "person", title: "Your move: talk about the plan", detail: "Keep the outbox?", question: false,
      next: "Talk about the plan" });
    expect(plan.act).toMatchObject({ kind: "link", href: "/series/sr-1?section=talk" });
    const env = moveOf("stuck", head, { ...pending, hold: hold("question_environment", { act: { kind: "answer", target: null } }) });
    expect(env).toMatchObject({ tone: "bad", title: "Reported to the admin", detail: "Keep the outbox?", question: true });
    expect(env.text).toBe("Attempt 1 asked about its environment. It is reported to the admin.");
  });

  it("yourMove_HandedBack_IsBlue", () => {
    const move = moveOf("needs_you", attempt({ handedBack: true }), { kind: "handback", handback: { text: "Which header?", at: "2026-09-04T14:00:00Z" },
      hold: hold("handback", { act: { kind: "conversation", target: null } }) });
    expect(move).toMatchObject({ tone: "person", title: "Your move: the ticket came back to you", detail: "Which header?", next: "Reply below" });
  });

  // 2026-11-23-d60d: no in-review state — a pull request that can merge is a merge hold, red CI a decision.
  it("yourMove_AMergeReadyPullRequest_IsBlue_RedCi_IsRedAndNamesTheCheck", () => {
    const ready = moveOf("needs_you", attempt(), { pullRequests: [pr({ state: "open" })],
      hold: hold("merge_ready", { act: { kind: "pull_request", target: "https://example.test/pr/809" } }) });
    expect(ready).toMatchObject({ tone: "person", title: "Your move: merge the pull request", text: "Its pull request can be merged now.",
      act: { kind: "link", label: "Open the pull request", href: "https://example.test/pr/809" }, next: "Open the pull request" });
    const red = moveOf("needs_you", attempt(), { pullRequests: [pr({ state: "open" })],
      hold: hold("ci_failed", { names: ["build"], act: { kind: "pull_request", target: "https://example.test/pr/809" } }) });
    expect(red).toMatchObject({ tone: "bad", title: "Your move: CI failed", text: "CI failed: build." });
  });

  it("yourMove_AMergeAndAChangedText_AreAPersonsMove", () => {
    expect(moveOf("needs_you", attempt(), failedHold(hold("merge_next", { act: { kind: "merge", target: "sr-1" } }))))
      .toMatchObject({ tone: "person", title: "Your move: merge the pull request", next: "Merge" });
    expect(moveOf("needs_you", attempt(), failedHold(hold("text_edited", { act: { kind: "conversation", target: null } }))))
      .toMatchObject({ tone: "person", title: "Your move: the ticket text changed", next: "Reply below" });
  });

  it("yourMove_Stuck_IsRed_TitledByTheEndKind_WithWhy", () => {
    const image = moveOf("stuck", attempt({ status: "failed", endKind: "image-untrusted" }),
      failedHold(hold("image_untrusted", { act: { kind: "settings", target: "/config/settings/sandbox" } })));
    expect(image).toMatchObject({ tone: "bad", title: "Stuck: an image registry is not trusted", why: true });
    expect(image.act).toMatchObject({ kind: "link", href: "/config/settings/sandbox" });
    expect(moveOf("stuck", attempt({ status: "failed", endKind: "bootstrap-missing" }),
      failedHold(hold("bootstrap_missing", { act: { kind: "init", target: "shop" } }))).act).toMatchObject({ kind: "init" });
    expect(moveOf("needs_you", attempt({ status: "failed", endedOnInfrastructure: true }),
      failedHold(hold("infrastructure", { act: { kind: "retry", target: "r-1" } }))).title).toBe("Stopped: the sandbox or host was lost");
    expect(moveOf("needs_you", attempt({ status: "failed" }), failedHold(hold("failed", { act: { kind: "conversation", target: null } }))).title)
      .toBe("Stopped");
  });

  it("yourMove_AnEndDetail_ReplacesTheHeadlineAndTheInstruction_TheActStaysTheHolds", () => {
    const endDetail = { subject: "registry ghcr.example/tools is not trusted", repos: [], control: "Trusted image registries",
      setTo: "ghcr.example/tools", settingsPath: "/config/settings/sandbox" };
    const d = detail([attempt({ status: "failed", endKind: "preflight", endDetail })]);
    d.summary = { ...d.summary, bucket: "stuck", hold: hold("preflight", { act: { kind: "settings", target: "/config/settings/sandbox" }, detail: endDetail }) };
    const view = ticketState({ detail: d, liveEvents: [], series: null })!;
    const move = yourMove(view, "stuck", endDetail);
    expect(move.title).toBe("Stuck: registry ghcr.example/tools is not trusted");
    expect(move.text).toBe(view.sentence);
    expect(move.detail).toBe("Set Trusted image registries to ghcr.example/tools under /config/settings/sandbox. It restarts by itself once you save.");
    expect(move.act).toEqual({ kind: "link", label: "Open the setting", href: "/config/settings/sandbox" });
  });

  it("yourMove_Done_IsGreen_Closed_IsQuiet_Running_IsAmber", () => {
    expect(moveOf("done")).toMatchObject({ tone: "ok", title: "Done", act: null });
    expect(moveOf("closed")).toMatchObject({ tone: "quiet", title: "Closed" });
    expect(moveOf("working", attempt({ status: "running", finishedAt: null }))).toMatchObject({ tone: "amber", title: "Working. Nothing for you to do.", next: "Nothing for you to do" });
    expect(moveOf("working", attempt({ status: "queued", finishedAt: null })).title).toBe("Queued. Nothing for you to do.");
    expect(moveOf("working", attempt({ status: "waiting_for_input", finishedAt: null })).title).toBe("Resuming. Nothing for you to do.");
  });

  it("yourMove_PullRequestsClosed_IsRed", () => {
    expect(moveOf("pr_closed")).toMatchObject({ tone: "bad", title: "Stopped: the pull requests were closed", why: true });
  });

  it("YourMove_SeveralOpenPullRequestsReadyToMerge_DrawNoSingleAct", () => {
    const two = moveOf("needs_you", attempt(), { pullRequests: [pr({ state: "open" }), pr({ state: "open", url: "https://example.test/pr/2" })],
      hold: hold("merge_ready", { act: { kind: "pull_request", target: null } }) });
    expect(two).toMatchObject({ act: null, next: "Open the pull requests below", title: "Your move: merge the pull request" });
  });

  it("YourMove_ACancelledHeadWaitingToMerge_StaysCancelled", () => {
    expect(moveOf("failed", attempt({ status: "cancelled" }), { hold: hold("merge_after", { names: ["#1"] }) }))
      .toMatchObject({ tone: "quiet", title: "Cancelled" });
    expect(moveOf("failed", attempt({ status: "cancelled" }), { hold: hold("restarting") }))
      .toMatchObject({ tone: "amber", title: "Nothing for you to do", text: "Restarts after the server stopped. Nothing for you to do." });
  });

  it("YourMove_AMergeAfterAnother_IsNothingForYouYet", () => {
    const after = moveOf("working", attempt(), { pullRequests: [pr({ state: "open" })], hold: hold("merge_after", { names: ["SCRUM-1"] }) });
    expect(after).toMatchObject({ tone: "amber", title: "Nothing for you to do", act: null, next: "Nothing for you to do" });
    const ci = moveOf("working", attempt(), { pullRequests: [pr({ state: "open" })], hold: hold("ci_running", { since: new Date().toISOString() }) });
    expect(ci.title).toBe("Nothing for you to do");
    expect(ci.text).toMatch(/^CI has been running since \d\d:\d\d\. Nothing for you to do\.$/);
  });

  it("YourMove_Tones_FollowThePalette", () => {
    const head = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: QUESTION });
    expect(moveOf("needs_you", head, { kind: "question", pendingQuestion: { runId: "r-1", question: QUESTION }, hold: hold("question") }).tone).toBe("person");
    expect(moveOf("working", attempt({ status: "running", finishedAt: null })).tone).toBe("amber");
    expect(moveOf("failed", attempt({ status: "cancelled" }))).toMatchObject({ tone: "quiet", title: "Cancelled" });
    expect(moveOf("failed", attempt({ status: "cancelled", endKind: "series-paused" }))).toMatchObject({ tone: "quiet", title: "Paused with its series" });
    expect(moveOf("needs_you", attempt({ status: "failed" }), failedHold(hold("failed"))).tone).toBe("bad");
    expect(moveOf("done").tone).toBe("ok");
  });

  it("ActLabel_EachActKind_MatchesTheYourMoveButtonText", () => {
    expect(actLabel({ kind: "retry", attempt: attempt() }, false)).toBe("Retry unchanged");
    expect(actLabel({ kind: "init", project: "shop" }, false)).toBe("Initialize the project");
    expect(actLabel({ kind: "merge", seriesId: "sr-1", ticketId: "101" }, false)).toBe("Merge");
    expect(actLabel({ kind: "link", label: "Open the setting", href: "/x" }, false)).toBe("Open the setting");
    expect(actLabel({ kind: "tab", label: "Continue in conversation", tab: "conversation" }, true)).toBe("Answer the question");
    expect(actLabel({ kind: "tab", label: "Continue in conversation", tab: "conversation" }, false)).toBe("Reply below");
  });

  // 2026-11-23-31dci: a wait reported to the admin is no stop — amber, titled so, with the hold's sentence.
  it("YourMove_AnEscalatedWait_SaysItWasReportedToTheAdmin", () => {
    const head = attempt({ status: "running", finishedAt: null });
    const escalated = hold("queued", { addressee: "admin", since: "2026-11-23T00:12:00Z",
      escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } });
    const move = moveOf("stuck", head, { hold: escalated });
    expect(move.tone).toBe("amber");
    expect(move.title).toBe("Reported to the admin");
    expect(move.why).toBe(false);
    expect(move.text).toContain("Reported to the admin.");
    expect(move.text).toContain("holds.escalate_after_minutes");
  });
});
