import { describe, expect, it } from "vitest";
import type { TicketServerBucket } from "@/types/tickets";
import { ticketState } from "../ticketState";
import { actLabel, yourMove } from "../yourMove";
import { attempt, detail } from "./detailFixtures";
import { pr } from "../../__tests__/ticketFixtures";

// 2026-11-16-6877a: the Your move box reads ticketState's view; each state gets its colour, one title and its act.

const QUESTION = {
  questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null,
  askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: "2026-09-05T14:10:00Z",
};

function moveOf(bucket: TicketServerBucket, head = attempt(), over: Partial<ReturnType<typeof detail>["summary"]> = {}) {
  const d = detail([head]);
  d.summary = { ...d.summary, bucket, ...over };
  const view = ticketState({ detail: d, liveEvents: [], series: null })!;
  return yourMove(view, bucket, d.summary.kind);
}

describe("yourMove", () => {
  // 2026-11-22-7103a: a question or a hand-back is a person's move — blue.
  it("yourMove_WaitingForAnAnswer_IsBlue_AndAsksForTheAnswer", () => {
    const head = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: QUESTION });
    const move = moveOf("needs_you", head, { kind: "question", pendingQuestion: { runId: "r-1", question: QUESTION } });
    expect(move).toMatchObject({ tone: "person", title: "Your move: answer the question", detail: "Keep the outbox?", why: false, next: "Answer the question" });
    expect(move.act).toMatchObject({ kind: "tab", tab: "conversation" });
  });

  it("yourMove_HandedBack_IsBlue", () => {
    const move = moveOf("needs_you", attempt({ handedBack: true }), { kind: "handback", handback: { text: "Which header?", at: "2026-09-04T14:00:00Z" } });
    expect(move).toMatchObject({ tone: "person", title: "Your move: the ticket came back to you", detail: "Which header?", next: "Reply below" });
  });

  it("yourMove_InReview_IsBlue", () => {
    const move = moveOf("waiting_on_review", attempt(), { pullRequests: [pr({ state: "open" })] });
    expect(move).toMatchObject({ tone: "person", title: "Your move: review the pull request", text: "Waiting on review: 1 pull request is open." });
  });

  it("yourMove_Stuck_IsRed_TitledByTheEndKind_WithWhy", () => {
    const move = moveOf("stuck", attempt({ status: "failed", endKind: "image-untrusted" }));
    expect(move).toMatchObject({ tone: "bad", title: "Stuck: an image registry is not trusted", why: true });
    expect(move.act).toMatchObject({ kind: "link", href: "/config/settings/sandbox" });
    expect(moveOf("failed", attempt({ status: "failed", endKind: "bootstrap-missing" })).act).toMatchObject({ kind: "init" });
    expect(moveOf("failed", attempt({ status: "failed", endedOnInfrastructure: true })).title).toBe("Stopped: the sandbox or host was lost");
    expect(moveOf("failed", attempt({ status: "failed" })).title).toBe("Stopped");
  });

  it("yourMove_AnEndDetail_ReplacesTheHeadlineAndTheInstruction", () => {
    const d = detail([attempt({ status: "failed", endKind: "preflight" })]);
    d.summary = { ...d.summary, bucket: "stuck" };
    const view = ticketState({ detail: d, liveEvents: [], series: null })!;
    const move = yourMove(view, "stuck", null, {
      subject: "registry ghcr.example/tools is not trusted", control: "Trusted image registries", setTo: "ghcr.example/tools",
      settingsPath: "/config/settings/sandbox",
    });
    expect(move.title).toBe("Stuck: registry ghcr.example/tools is not trusted");
    expect(move.text).toBe("Set Trusted image registries to ghcr.example/tools under /config/settings/sandbox. It restarts by itself once you save.");
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

  it("YourMove_OneOpenPullRequestInReview_CarriesTheReviewAct", () => {
    const one = moveOf("waiting_on_review", attempt(), { pullRequests: [pr({ state: "open" })] });
    expect(one.act).toMatchObject({ kind: "link", label: "Review the pull request" });
    expect(one.next).toBe("Review the pull request");
    const two = moveOf("waiting_on_review", attempt(), { pullRequests: [pr({ state: "open" }), pr({ state: "open", url: "https://example.test/pr/2" })] });
    expect(two.act).toBeNull();
    expect(two.next).toBe("Review the pull requests");
  });

  it("YourMove_Tones_FollowThePalette", () => {
    const head = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: QUESTION });
    expect(moveOf("needs_you", head, { kind: "question", pendingQuestion: { runId: "r-1", question: QUESTION } }).tone).toBe("person");
    expect(moveOf("working", attempt({ status: "running", finishedAt: null })).tone).toBe("amber");
    expect(moveOf("failed", attempt({ status: "cancelled" }))).toMatchObject({ tone: "quiet", title: "Cancelled" });
    expect(moveOf("failed", attempt({ status: "cancelled", endKind: "series-paused" }))).toMatchObject({ tone: "quiet", title: "Paused with its series" });
    expect(moveOf("failed", attempt({ status: "failed" })).tone).toBe("bad");
    expect(moveOf("done").tone).toBe("ok");
  });

  it("ActLabel_EachActKind_MatchesTheYourMoveButtonText", () => {
    expect(actLabel({ kind: "retry", attempt: attempt() }, false)).toBe("Retry unchanged");
    expect(actLabel({ kind: "init", project: "shop" }, false)).toBe("Initialize the project");
    expect(actLabel({ kind: "link", label: "Open the setting", href: "/x" }, false)).toBe("Open the setting");
    expect(actLabel({ kind: "tab", label: "Continue in conversation", tab: "conversation" }, true)).toBe("Answer the question");
    expect(actLabel({ kind: "tab", label: "Continue in conversation", tab: "conversation" }, false)).toBe("Reply below");
  });
});
