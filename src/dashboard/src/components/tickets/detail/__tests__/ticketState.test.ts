import { describe, expect, it } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { TicketDetail, TicketHold, TicketSummary } from "@/types/tickets";
import { translator } from "@/lib/i18n/translate";
import { hold, pr } from "../../__tests__/ticketFixtures";
import { CONTINUE, ticketState } from "../ticketState";
import { attempt, detail, series, spec } from "./detailFixtures";

// 2026-10-16-86e6c: one sentence, one act chosen from recorded fields, and the run the sentence is about.
// 2026-11-23-31dcg: a held ticket's sentence and act are its hold's, read on the server; the view only draws them.

const SANDBOX = "/config/settings/sandbox";

function heldTicket(held: TicketHold, over: Parameters<typeof attempt>[0] = {}, bucket: TicketSummary["bucket"] = "needs_you"): TicketDetail {
  const d = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed", ...over })]);
  d.summary = { ...d.summary, bucket, hold: { ...held, attempt: 2 } };
  return d;
}

function endedTicket(over: Parameters<typeof attempt>[0] = {}): TicketDetail {
  const d = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed", ...over })]);
  d.summary = { ...d.summary, bucket: "failed", hold: null };
  return d;
}

const state = (d: TicketDetail, liveEvents: RunEvent[] = [], cut = null as Parameters<typeof ticketState>[0]["series"]) =>
  ticketState({ detail: d, liveEvents, series: cut })!;

describe("ticketState", () => {
  // 2026-11-19-1b72a: a head its paused series held offers nothing to continue — the resume starts it.
  it("ticketState_HeldByAPausedSeries_IsPausedWithNoAct", () => {
    const view = state(endedTicket({ status: "cancelled", endKind: "series-paused" }));
    expect(view).toMatchObject({ tone: "q", pill: "Paused", act: null });
    expect(view.sentence).toBe("Its series is paused; it starts again when the series resumes.");
  });

  it("ticketState_FailedOnInfrastructure_OffersRetryUnchanged", () => {
    const view = state(heldTicket(hold("infrastructure", { act: { kind: "retry", target: "r-2" } }), { endedOnInfrastructure: true }));
    expect(view.tone).toBe("bad");
    expect(view.sentence).toBe("Attempt 2 ended on infrastructure: the sandbox or host was lost, not the work.");
    expect(view.act).toMatchObject({ kind: "retry", attempt: { runId: "r-2" } });
    expect(view.attempt?.runId).toBe("r-2");
  });

  it("ticketState_ImageUntrusted_LinksTheSandboxSettings", () => {
    const view = state(heldTicket(hold("image_untrusted", { act: { kind: "settings", target: SANDBOX } }), { endKind: "image-untrusted" }, "stuck"));
    expect(view.sentence).toContain("outside the trusted registries");
    expect(view.act).toMatchObject({ kind: "link", label: "Trust the registry in Settings", href: SANDBOX });
  });

  it("ticketState_BootstrapMissing_OffersInitProject", () => {
    const view = state(heldTicket(hold("bootstrap_missing", { act: { kind: "init", target: "shop" } }), { endKind: "bootstrap-missing" }, "stuck"));
    expect(view.sentence).toContain("no agent-smith bootstrap");
    expect(view.act).toEqual({ kind: "init", project: "shop" });
  });

  it("ticketState_ImageUntrustedWithEndDetail_NamesTheImageAndTheSetting", () => {
    const view = state(heldTicket(hold("image_untrusted", { act: { kind: "settings", target: SANDBOX }, detail: {
      subject: "Image node:22-bookworm (context default)", repos: ["web"],
      control: "Trusted image registries", setTo: "ghcr.io/, docker.io/library/", settingsPath: SANDBOX } }), {}, "stuck"));
    expect(view.sentence).toBe(
      "Image node:22-bookworm (context default) is not trusted: set Trusted image registries to ghcr.io/, docker.io/library/.");
    expect(view.act).toMatchObject({ kind: "link", href: SANDBOX });
  });

  it("ticketState_BootstrapMissingWithEndDetail_NamesTheRepos", () => {
    const view = state(heldTicket(hold("bootstrap_missing", { act: { kind: "init", target: "shop" }, detail: {
      subject: "shop", repos: ["api", "web"], control: "Initialise", setTo: "shop", settingsPath: "/config/projects" } }), {}, "stuck"));
    expect(view.sentence).toBe("api, web have no agent-smith bootstrap: Initialise project shop.");
    expect(view.act).toEqual({ kind: "init", project: "shop" });
  });

  it("ticketState_PreflightWithEndDetail_NamesTheKey", () => {
    const view = state(heldTicket(hold("preflight", { act: { kind: "settings", target: "/config/agents" }, detail: {
      subject: "The pipeline's agent", repos: [], control: "agents.<name>.model", setTo: "a model", settingsPath: "/config/agents" } }), {}, "stuck"));
    expect(view.sentence).toBe("The pipeline's agent: set agents.<name>.model to a model.");
    expect(view.act).toMatchObject({ kind: "link", label: "Open the setting", href: "/config/agents" });
  });

  it("ticketState_ReadinessUnknown_OffersRetry", () => {
    const view = state(heldTicket(hold("readiness_unknown", { act: { kind: "retry", target: "r-2" }, detail: {
      subject: "api: host down", repos: ["api"], control: null, setTo: null, settingsPath: null } })));
    expect(view.sentence).toBe("Attempt 2 could not read the repositories' bootstrap from the host: api: host down");
    expect(view.act).toMatchObject({ kind: "retry" });
  });

  it("ticketState_StoredCriteriaUnmet_OffersTheNextAttemptNotAnOverrule", () => {
    const view = state(heldTicket(hold("criteria_unmet", { act: { kind: "conversation", target: null } })));
    expect(view.act).toEqual(CONTINUE);
  });

  it("ticketState_UnclassifiedFailure_OffersContinueInConversation", () => {
    const view = state(heldTicket(hold("failed", { act: { kind: "conversation", target: null } }), { summary: "Sandbox pod p could not start: " }));
    expect(view.sentence).toBe("Attempt 2 failed.");
    expect(view.act).toEqual(CONTINUE);
  });

  it("ticketState_NeedsYouQuestion_ShowsTheQuestionAndAnswer", () => {
    const d = detail([attempt({ status: "waiting_for_input" })]);
    d.summary = { ...d.summary, bucket: "needs_you", kind: "question", hold: hold("question", { act: { kind: "answer", target: null } }),
      pendingQuestion: { runId: "r-1", question: {
        questionId: "q1", type: "text", text: "Which queues listen?", context: null, choices: [], defaultAnswer: null,
        askedAt: "2026-09-04T14:00:00Z", answerDeadlineAt: "2026-09-07T14:00:00Z" } } };
    const view = state(d);
    expect(view.tone).toBe("person");
    expect(view.sentence).toBe("Attempt 1 is waiting for your answer.");
    expect(view.detail).toBe("Which queues listen?");
    expect(view.act).toEqual({ kind: "tab", label: "Answer in the conversation", tab: "conversation" });
  });

  // 2026-11-23-31dcg: the merge a series holds the ticket for, and the series' Merge next a start waits for.
  it("ticketState_AMergeHold_OffersTheSeriesMerge_AStartAfterMerges_OpensMergeNext", () => {
    const d = detail([attempt()]);
    d.summary = { ...d.summary, bucket: "needs_you", hold: hold("merge_next", { act: { kind: "merge", target: "sr-1" } }) };
    expect(state(d)).toMatchObject({ tone: "person", sentence: "Its pull request is next in the series' merge order.",
      act: { kind: "merge", seriesId: "sr-1", ticketId: "101" } });
    const never = detail([]);
    never.summary = { ...never.summary, bucket: "needs_you", hold: hold("starts_on_merge", { names: ["#1", "#2"], act: { kind: "merge_next", target: "sr-1" } }) };
    expect(state(never)).toMatchObject({ sentence: "Starts once #1 and #2 are merged, in that order.",
      act: { kind: "link", href: "/series/sr-1?section=next" } });
  });

  it("ticketState_AHold_SpeaksTheViewersLanguage", () => {
    const d = heldTicket(hold("failed", { act: { kind: "conversation", target: null } }));
    expect(ticketState({ detail: d, liveEvents: [], series: null, t: translator("de") })!.sentence).toBe("Versuch 2 ist fehlgeschlagen.");
  });

  it("ticketState_WorkingRunning_SaysSpecStepAndTheLiveLine", () => {
    const d = detail([attempt({ status: "running", finishedAt: null })]);
    d.summary = { ...d.summary, bucket: "working", hold: hold("working") };
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

  it("ticketState_InReviewAfterAnotherMerge_SaysItFromItsHold", () => {
    const d = detail([attempt()]);
    d.summary = { ...d.summary, bucket: "waiting_on_review", hold: hold("merge_after", { names: ["SCRUM-1"] }), pullRequests: [pr()] };
    expect(state(d)).toMatchObject({ sentence: "Merges after SCRUM-1. Nothing for you to do yet.", act: null });
  });

  it("ticketState_FailedWithOpenPullRequests_LeadsWithTheFailureAndCarriesThePullRequests", () => {
    const d = heldTicket(hold("criteria_unmet", { act: { kind: "conversation", target: null } }));
    d.summary = { ...d.summary, pullRequests: [pr({ number: "810", state: "open" })] };
    const view = state(d);
    expect(view.sentence).toBe("Attempt 2 failed: the verifier of that time did not accept a criterion.");
    expect(view.pullRequests.map((p) => p.number)).toEqual(["810"]);
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

  // 2026-11-17-f9b0a: the operator's answer to contract Q2.
  it("ticketState_CancelledHeadInFailed_SaysCancelled", () => {
    const view = state(endedTicket({ status: "cancelled" }));
    expect(view.pill).toBe("Cancelled");
    expect(view.sentence).toBe("Attempt 2 was cancelled.");
    expect(view.act).toEqual(CONTINUE);
  });

  it("ticketState_DoneWithoutPullRequest_NamesTheAttempt", () => {
    const d = detail([attempt({ status: "success" })]);
    d.summary = { ...d.summary, bucket: "done", pullRequests: [] };
    expect(state(d).sentence).toBe("Done — attempt 1 finished without a pull request.");
  });

  it("ticketState_DoneWithMergedPullRequest_SaysMerged", () => {
    const d = detail([attempt({ status: "success" })]);
    d.summary = { ...d.summary, bucket: "done", pullRequests: [pr()] };
    expect(state(d).sentence).toBe("Done — the pull requests are merged.");
  });
});
