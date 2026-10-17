import { describe, expect, it } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { TicketDetail, TicketSummary } from "@/types/tickets";
import { pr } from "../../__tests__/ticketFixtures";
import { CONTINUE, SANDBOX_SETTINGS_PATH, ticketState } from "../ticketState";
import { attempt, detail, series, spec } from "./detailFixtures";

// 2026-10-16-86e6c: one sentence, one act chosen from recorded fields, and the run the sentence is about.

function failedTicket(over: Parameters<typeof attempt>[0] = {}, bucket: TicketSummary["bucket"] = "failed"): TicketDetail {
  const d = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed", ...over })]);
  d.summary = { ...d.summary, bucket };
  return d;
}

const state = (d: TicketDetail, liveEvents: RunEvent[] = [], cut = null as Parameters<typeof ticketState>[0]["series"]) =>
  ticketState({ detail: d, liveEvents, series: cut })!;

describe("ticketState", () => {
  it("ticketState_FailedOnInfrastructure_OffersRetryUnchanged", () => {
    const view = state(failedTicket({ endedOnInfrastructure: true }));
    expect(view.tone).toBe("bad");
    expect(view.sentence).toBe("Attempt 2 ended on infrastructure: the sandbox or host was lost, not the work.");
    expect(view.act).toMatchObject({ kind: "retry" });
    expect(view.attempt?.runId).toBe("r-2");
  });

  it("ticketState_ImageUntrusted_LinksTheSandboxSettings", () => {
    const view = state(failedTicket({ endKind: "image-untrusted" }));
    expect(view.sentence).toContain("outside the trusted registries");
    expect(view.act).toMatchObject({ kind: "link", label: "Trust the registry in Settings", href: SANDBOX_SETTINGS_PATH });
  });

  it("ticketState_BootstrapMissing_OffersInitProject", () => {
    const view = state(failedTicket({ endKind: "bootstrap-missing" }));
    expect(view.sentence).toContain("no agent-smith bootstrap");
    expect(view.act).toEqual({ kind: "init", project: "shop" });
  });

  it("ticketState_CriteriaUnmet_LinksTheOverrule", () => {
    const view = state(failedTicket({ endKind: "criteria-unmet" }));
    expect(view.act).toMatchObject({ kind: "link", label: "Overrule in the run", href: "/jobs/r-2" });
    expect(view.act && "note" in view.act ? view.act.note : "").toContain("not re-run");
  });

  it("ticketState_UnclassifiedFailure_OffersContinueInConversation", () => {
    const view = state(failedTicket({ summary: "Sandbox pod p could not start: " }, "stuck"));
    expect(view.pill).toBe("Stuck");
    expect(view.sentence).toBe("Attempt 2 failed.");
    expect(view.act).toEqual(CONTINUE);
  });

  it("ticketState_NeedsYouQuestion_ShowsTheQuestionAndAnswer", () => {
    const d = detail([attempt({ status: "waiting_for_input" })]);
    d.summary = { ...d.summary, bucket: "needs_you", kind: "question", pendingQuestion: { runId: "r-1", question: {
      questionId: "q1", type: "text", text: "Which queues listen?", context: null, choices: [], defaultAnswer: null,
      askedAt: "2026-09-04T14:00:00Z", answerDeadlineAt: "2026-09-07T14:00:00Z" } } };
    const view = state(d);
    expect(view.tone).toBe("person");
    expect(view.sentence).toBe("Attempt 1 is waiting for your answer.");
    expect(view.detail).toBe("Which queues listen?");
    expect(view.act).toEqual({ kind: "tab", label: "Answer in the conversation", tab: "conversation" });
  });

  it("ticketState_WorkingRunning_SaysSpecStepAndTheLiveLine", () => {
    const d = detail([attempt({ status: "running", finishedAt: null })]);
    d.summary = { ...d.summary, bucket: "working" };
    const cut = series("r-1", [spec({ state: "in_progress", steps: [
      { id: "a", action: "Upgrade the package", target: null }, { id: "b", action: "Adapt the consumers", target: null }],
      worked: [{ stepId: "a", state: "done", doneByRunId: "r-1", doneByAttempt: 1, doneInPass: 1 }] })]);
    const events = [{ type: EventType.StepStarted, runId: "r-1", timestamp: "", stepIndex: 9, stepName: "AgenticMasterCommand",
      totalSteps: 20, displayName: "Working the spec" }] as unknown as RunEvent[];
    const view = state(d, events, cut);
    expect(view.sentence).toBe("Spec 1 of 1, step 2 of 2: Adapt the consumers");
    expect(view.detail).toBe("Now: Working the spec");
    expect(view.act).toBeNull();
    expect(view.quiet).toBe(true);
  });

  it("ticketState_WorkingResumingOrDeriving_SaysSo", () => {
    const parked = detail([attempt({ status: "waiting_for_input", finishedAt: null })]);
    parked.summary = { ...parked.summary, bucket: "working", queuePlace: 2 };
    expect(state(parked).sentence).toBe("Attempt 1 is resuming — place 2 in line.");
    const running = detail([attempt({ status: "running", finishedAt: null })]);
    running.summary = { ...running.summary, bucket: "working" };
    expect(state(running).sentence).toBe("Attempt 1 is deriving the series.");
  });

  it("ticketState_WaitingOnReview_HasNoActAndCarriesThePullRequests", () => {
    const d = detail([attempt()]);
    d.summary = { ...d.summary, bucket: "waiting_on_review", pullRequests: [pr({ state: "merged" }), pr({ number: "810", state: "open", url: "https://github.com/o/api/pull/810" })] };
    const view = state(d);
    expect(view.sentence).toBe("Waiting on review: 1 pull request is open.");
    expect(view.act).toBeNull();
    expect(view.pullRequests.map((p) => p.number)).toEqual(["809", "810"]);
  });

  it("ticketState_FailedWithOpenPullRequests_LeadsWithTheFailureAndCarriesThePullRequests", () => {
    const d = failedTicket({ endKind: "criteria-unmet" });
    d.summary = { ...d.summary, pullRequests: [pr({ number: "810", state: "open" })] };
    const view = state(d);
    expect(view.pill).toBe("Failed");
    expect(view.sentence).toBe("Attempt 2 failed: the acceptance gate did not accept a criterion.");
    expect(view.pullRequests.map((p) => p.number)).toEqual(["810"]);
  });

  it("ticketState_FailureActs_HaveNoOtherOptions", () => {
    for (const over of [{ endedOnInfrastructure: true }, { endKind: "image-untrusted" }, { endKind: "criteria-unmet" }, {}]) {
      expect(Object.keys(state(failedTicket(over)))).not.toContain("others");
    }
  });

  it("ticketState_DoneClosedIdle_AreOneQuietLineWithoutAct", () => {
    for (const bucket of ["done", "closed", "idle"] as const) {
      const d = detail([attempt({ status: "cancelled" })]);
      d.summary = { ...d.summary, bucket, kind: bucket === "closed" ? "tracker" : null };
      const view = state(d);
      expect(view.quiet).toBe(true);
      expect(view.act).toBeNull();
    }
  });
});
