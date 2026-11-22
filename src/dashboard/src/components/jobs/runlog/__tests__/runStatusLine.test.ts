import { describe, expect, it } from "vitest";
import type { YourMove } from "@/components/tickets/detail/yourMove";
import type { TicketAttempt } from "@/types/tickets";
import {
  attemptStatus, attemptStatusInput, nextStep, retryAlso, runStatus, statusSentence, type StatusContext, type StatusInput,
  type StatusSpec,
} from "../runStatusLine";

// 2026-11-22-7103a: a run's one status line — what happened, then the next step — from recorded fields only. Each
// sentence is asserted whole.

const spec = (specId: string, ordinal: number, status: string, verdict: string | null = null): StatusSpec =>
  ({ specId, ordinal, status, verdict });

const input = (over: Partial<StatusInput> = {}): StatusInput => ({ status: "success", specs: [], pullRequests: [], ...over });

const attempt = (over: Partial<TicketAttempt> = {}): TicketAttempt => ({
  number: 2, runId: "r-2", status: "failed", startedAt: "2026-11-22T14:00:00Z", finishedAt: "2026-11-22T14:46:00Z", cost: 1,
  endedOnInfrastructure: false, handedBack: false, pullRequests: [], executedSpecs: [], pendingQuestion: null, ...over,
});

const move = (over: Partial<YourMove> = {}): YourMove => ({
  tone: "person", title: "Your move: review the pull request", text: "", detail: null, act: null, why: false, question: false,
  next: "Review the pull request", ...over,
});

const ctx = (over: Partial<StatusContext> = {}): StatusContext => ({ move: move(), latest: attempt(), canRetry: true, ...over });

const NO_REPORT = "The agent ended without writing a report, so the spec was not delivered. Retry unchanged gives it another attempt.";
const sentence = (i: StatusInput) => statusSentence(runStatus(i));

describe("runStatus", () => {
  it("RunStatus_ShortfallOneOfTwo_HeadCountsDeliveredAndClauseNamesSpecAndVerdict", () => {
    const line = runStatus(input({ status: "shortfall", specs: [spec("a1", 1, "done"), spec("b2", 2, "failed", NO_REPORT)], pullRequests: ["server"] }));
    expect(line).toMatchObject({ tone: "bad", head: "1 of 2 specs delivered", short: "1 of 2 specs delivered", next: null });
    expect(statusSentence(line)).toBe("1 of 2 specs delivered · b2: The agent ended without writing a report, so the spec was not delivered.");
  });

  it("RunStatus_ShortfallWithoutSpecRows_SaysNotEverythingWasDelivered", () => {
    expect(runStatus(input({ status: "shortfall", summary: "Two of three repositories pushed.\nmore" })))
      .toMatchObject({ tone: "bad", head: "Not everything was delivered", clauses: ["Two of three repositories pushed."] });
  });

  it("RunStatus_SuccessWithOpenPullRequest_SaysDeliveredAndNextReviewsThePullRequest", () => {
    expect(sentence(input({ specs: [spec("a1", 1, "done")], pullRequests: ["server"], next: "Review the pull request" })))
      .toBe("Delivered · Pull request opened in server. → Review the pull request.");
    expect(sentence(input({ specs: [spec("a1", 1, "done"), spec("b2", 2, "done")], pullRequests: ["a", "b"] })))
      .toBe("2 of 2 specs delivered · 2 pull requests opened.");
  });

  it("RunStatus_ShortfallInReview_NextReviewsAndNamesRetryForTheUndeliveredSpec", () => {
    expect(sentence(input({ status: "shortfall", specs: [spec("a1", 1, "done"), spec("b2", 2, "failed", NO_REPORT)],
      next: "Review the pull request", retryAlso: true })))
      .toBe("1 of 2 specs delivered · b2: The agent ended without writing a report, so the spec was not delivered. "
        + "→ Review the pull request; Retry unchanged gives b2 another attempt.");
  });

  it("RunStatus_FailedWithSpecVerdict_HeadIsFailedWithTheVerdictsFirstSentence", () => {
    const line = runStatus(input({ status: "failed", specs: [spec("a1", 1, "failed", "dotnet test exited 1. See the log.")], summary: "Other words." }));
    expect(line).toMatchObject({ tone: "bad", head: "Failed: dotnet test exited 1", short: "Failed" });
    expect(statusSentence(line)).toBe("Failed: dotnet test exited 1");
  });

  it("RunStatus_FailedWithEndDetail_UsesTheDetailSentence", () => {
    const a = attempt({ endKind: "image-untrusted", endDetail: { subject: "Image node:22", repos: [], control: "Trusted image registries", setTo: "docker.io/", settingsPath: "/config" } as never });
    expect(sentence({ ...attemptStatusInput(a), summary: "x" }))
      .toBe("Stopped before any work: Image node:22 is not trusted: set Trusted image registries to docker.io/");
  });

  it("RunStatus_EndKinds_SayWhyWithoutTheAttemptNumber", () => {
    const head = (endKind: string, over: Partial<TicketAttempt> = {}) => runStatus(attemptStatusInput(attempt({ endKind, ...over }))).head;
    expect(head("image-untrusted")).toBe("Stopped before any work: a context names a container image outside the trusted registries");
    expect(head("bootstrap-missing")).toBe("Stopped before any work: a repository has no agent-smith bootstrap");
    expect(head("preflight")).toBe("Stopped before any work: a precondition did not hold");
    expect(head("readiness-unknown", { endDetail: { subject: "api: host down" } as never }))
      .toBe("Stopped before any work: the repositories' bootstrap could not be read from the host: api: host down");
    expect(head("criteria-unmet")).toBe("Failed: the verifier of that time did not accept a criterion");
    expect(runStatus(attemptStatusInput(attempt({ endKind: "preflight" }))).short).toBe("Stopped before any work");
  });

  it("RunStatus_FailedBeforeAnySpec_NamesTheSummaryNotAttemptFailed", () => {
    expect(sentence(attemptStatusInput(attempt({ summary: "Checkout failed: the branch is gone.\nmore" }))))
      .toBe("Failed · Checkout failed: the branch is gone.");
    expect(sentence(input({ status: "failed", failedStep: "image pull back-off\nmore" }))).toBe("Failed: image pull back-off");
  });

  it("RunStatus_FailedWithNoCause_SaysFailedAlone", () => {
    expect(sentence(attemptStatusInput(attempt()))).toBe("Failed");
  });

  it("RunStatus_EndedOnInfrastructure_SaysTheHostWasLost", () => {
    expect(sentence(input({ status: "failed", endedOnInfrastructure: true }))).toBe("Stopped: the sandbox or host was lost");
  });

  it("RunStatus_WaitingForInput_IsBlue", () => {
    const asking = attemptStatusInput(attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: { questionId: "q" } as never }));
    expect(runStatus(asking)).toMatchObject({ tone: "person", head: "Waits for your answer" });
  });

  it("RunStatus_RunningOrQueued_IsAmber", () => {
    expect(runStatus(input({ status: "running", specs: [spec("a1", 1, "done"), spec("b2", 2, "in_progress")] })))
      .toMatchObject({ tone: "run", head: "Working on spec 2 of 2" });
    expect(runStatus(input({ status: "queued", queuePlace: 3 }))).toMatchObject({ tone: "run", head: "Queued, place 3" });
  });

  it("RunStatus_AnsweredAndWaitingForItsPlace_IsResuming", () => {
    const answered = attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: null });
    expect(runStatus(attemptStatusInput(answered, null, false, 2))).toMatchObject({ tone: "run", head: "Resuming, place 2" });
    expect(attemptStatus(answered, ctx({ latest: answered }), 4).head).toBe("Resuming, place 4");
    expect(attemptStatus(answered, ctx({ latest: attempt({ number: 3 }) }), 4).head).toBe("Resuming");
  });

  it("RunStatus_CancelledSupersededOrPaused_IsGrey", () => {
    expect(runStatus(input({ status: "cancelled" }))).toMatchObject({ tone: "quiet", head: "Cancelled" });
    expect(runStatus(input({ status: "cancelled", endKind: "superseded" }))).toMatchObject({ tone: "quiet", head: "Superseded" });
    expect(runStatus(input({ status: "cancelled", endKind: "series-paused" }))).toMatchObject({ tone: "quiet", head: "Paused with its series" });
  });

  it("RunStatus_HandedBack_IsBlue", () => {
    expect(sentence(input({ status: "success", handedBack: true, summary: "Which header? Both exist." }))).toBe("Handed back · Which header?");
  });

  it("RunStatus_DoneSpecsOnStoppedRun_SaysVerifiedNotDelivered", () => {
    expect(sentence(input({ status: "failed", specs: [spec("a1", 1, "done"), spec("b2", 2, "done")], summary: "Pull request refused." })))
      .toBe("Failed: Pull request refused · a1, b2 verified, not delivered.");
  });

  it("RunStatus_SuccessWithoutSpecRows_SaysDone", () => {
    expect(runStatus(input())).toEqual({ tone: "ok", head: "Done", short: "Done", clauses: [], next: null });
  });

  it("RunStatus_NoMove_OmitsTheNextStep", () => {
    expect(sentence(input({ status: "shortfall", specs: [spec("a1", 1, "done"), spec("b2", 2, "failed")], retryAlso: true })))
      .toBe("1 of 2 specs delivered · b2 not delivered.");
  });

  it("RunStatus_AnyCase_OwnWordsNeverSayFinishSpecOrReported", () => {
    const cases: StatusInput[] = ["success", "shortfall", "failed", "error", "cancelled", "running", "queued", "waiting_for_input"]
      .flatMap((status) => [input({ status }), input({ status, specs: [spec("a1", 1, "done"), spec("b2", 2, "failed")], pullRequests: ["s"], next: "x" })]);
    for (const c of cases) {
      const text = statusSentence(runStatus(c));
      expect(text).not.toMatch(/finish_spec|reported/i);
      expect(text).not.toMatch(/\.\./);
    }
  });
});

describe("nextStep", () => {
  it("NextStep_OlderAttempt_NamesTheLatestRun", () => {
    expect(nextStep(ctx({ latest: attempt({ number: 3, runId: "r-3" }) }), attempt())).toBe("See run 3, the latest");
    expect(retryAlso(ctx({ latest: attempt({ number: 3 }) }), attempt({ status: "shortfall" }))).toBe(false);
  });

  it("NextStep_NoAct_SaysNothingForYouToDo", () => {
    expect(nextStep(ctx({ move: move({ next: "" }) }), attempt())).toBe("Nothing for you to do");
    expect(nextStep(ctx(), attempt())).toBe("Review the pull request");
    expect(nextStep(null, attempt())).toBeNull();
  });

  it("NextStep_RetryForAReaderWithoutRunsControl_IsNotNamed", () => {
    const retry = move({ act: { kind: "retry", attempt: attempt() }, next: "Retry unchanged" });
    expect(nextStep(ctx({ move: retry }), attempt())).toBe("Retry unchanged");
    expect(nextStep(ctx({ move: retry, canRetry: false }), attempt())).toBeNull();
    expect(retryAlso(ctx(), attempt({ status: "shortfall" }))).toBe(true);
    expect(retryAlso(ctx({ canRetry: false }), attempt({ status: "shortfall" }))).toBe(false);
    expect(retryAlso(ctx({ move: retry }), attempt({ status: "shortfall" }))).toBe(false);
    // retriable's own rule: an unfinished or handed-back attempt is not retried.
    expect(retryAlso(ctx(), attempt({ status: "shortfall", finishedAt: null }))).toBe(false);
  });
});

describe("runStatus wording", () => {
  it("RunStatus_CauseThatSaysWhatFailed_FollowsABareFailed", () => {
    const line = runStatus(input({ status: "failed", summary: "Preflight failed: image not trusted.\nmore" }));
    expect(line).toMatchObject({ head: "Failed", short: "Failed", clauses: ["Preflight failed: image not trusted."] });
    expect(statusSentence(line)).toBe("Failed · Preflight failed: image not trusted.");
    expect(statusSentence(line)).not.toMatch(/Failed: \S+ failed/);
  });
});
