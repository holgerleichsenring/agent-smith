import { translator } from "@/lib/i18n/translate";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { TicketAttempt, TicketHold as RunTicketHold } from "@/types/tickets";

// 2026-11-17-6d9ea: the Now block follows the agent from the hub's events, asks the question while waiting, and once
// the run has ended says how, with its acts: pull requests, Retry unchanged, the ticket, why.

vi.mock("@/hooks/useSandboxActivity", () => ({ useSandboxActivity: () => null }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => false }));
const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
const retryAttempt = vi.fn().mockResolvedValue({ kind: "requested" });
vi.mock("@/lib/ticketsApi", async (original) => ({ ...(await original<object>()), retryAttempt: (id: string) => retryAttempt(id) }));

const { RunNow } = await import("../RunNow");
const { snapshotStatus: snapshotStatusIn } = await import("../runStatusLine");

const en = translator("en");
type DropLead<P extends unknown[], N extends number> = N extends 1 ? (P extends [unknown, ...infer R] ? R : never)
  : P extends [unknown, unknown, ...infer R] ? R : never;
const snapshotStatus = (...args: DropLead<Parameters<typeof snapshotStatusIn>, 1>) => snapshotStatusIn(en, ...args);

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:00:00Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: "Run master skill", totalSteps: 12,
    lastEventType: null, costUsd: 1.5, llmCalls: 12, ticketId: "13", ticketTitle: null, agentName: null,
    cancelRequested: false, ...over,
  };
}

const decision = (chose: string, timestamp: string): RunEvent =>
  ({ runId: "r1", type: EventType.DecisionLogged, timestamp, category: "c", chose, over: null, reason: "r" });

const attempt = { number: 2, runId: "r1", status: "failed", finishedAt: "2026-10-13T07:00:00Z", handedBack: false,
  endedOnInfrastructure: false } as unknown as TicketAttempt;
const reread = vi.fn();
const ticket = { label: "GH-13", href: "/tickets/shop/13", project: "shop", number: 2, of: 2, attempt, newest: true, latest: attempt, move: null, hold: null, reread };
const third = { ...attempt, number: 3, runId: "r3", status: "running", finishedAt: null } as unknown as TicketAttempt;
const ended = () => snap({ status: "failed", finishedAt: "2026-10-13T07:00:00Z" });

describe("RunNow", () => {
  it("RunNow_LiveRun_FollowsNewHubEvents", () => {
    const first = [decision("Use the outbox", "2026-10-13T06:10:00Z")];
    const { rerender } = render(<RunNow runId="r1" snapshot={snap()} events={first} steps={[]} ticket={null} />);
    expect(screen.getByTestId("run-now-label")).toHaveTextContent("Now — follows the agent");
    expect(screen.getAllByTestId("run-now-line")[1]).toHaveTextContent("Use the outbox");
    rerender(<RunNow runId="r1" snapshot={snap()} events={[...first, decision("Keep the queue", "2026-10-13T06:11:00Z")]} steps={[]} ticket={null} />);
    expect(screen.getAllByTestId("run-now-line")[1]).toHaveTextContent("Keep the queue");
  });

  it("RunNow_Finishing_SwitchesToHowItEnded", () => {
    const { rerender } = render(<RunNow runId="r1" snapshot={snap()} events={[]} steps={[]} ticket={null} />);
    rerender(<RunNow runId="r1" snapshot={snap({ status: "success", finishedAt: "2026-10-13T07:00:00Z", prUrl: "https://example.test/server/pull/7" })}
      events={[]} steps={[]} ticket={null} />);
    expect(screen.getByTestId("run-now-label")).toHaveTextContent("How it ended");
    expect(screen.getByTestId("run-now-pr")).toHaveAttribute("href", "https://example.test/server/pull/7");
    expect(screen.getByTestId("run-now-why")).toHaveAttribute("href", "/jobs/r1/why");
  });

  it("RunNow_Waiting_ShowsTheQuestionWithItsAnswerForm", () => {
    const question = { questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null,
      askedAt: "2026-10-13T06:10:00Z", answerDeadlineAt: "2026-10-14T06:10:00Z" };
    render(<RunNow runId="r1" snapshot={snap({ status: "waiting_for_input", pendingQuestion: question })} events={[]} steps={[]} ticket={null} />);
    expect(screen.getByTestId("run-now-label")).toHaveTextContent("Waiting for your answer");
    expect(within(screen.getByTestId("run-question")).getByTestId("pending-question-card")).toHaveTextContent("Keep the outbox?");
  });

  // 2026-11-27-3532e: a series-led question is its series master's — shown read-only, linking to the series chat, from the
  // run's own read before any ticket read; a series of one's question (a hold answered on the ticket) keeps its answer form.
  const parked = { questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null,
    askedAt: "2026-10-13T06:10:00Z", answerDeadlineAt: "2026-10-14T06:10:00Z" };

  it("RunNow_SeriesLedQuestion_IsReadOnly_WithALinkToTheSeries_BeforeTheTicketRead", () => {
    render(<RunNow runId="r1" snapshot={snap({ status: "waiting_for_input", pendingQuestion: { ...parked, seriesId: "sr-1" } })}
      events={[]} steps={[]} ticket={null} />);
    expect(screen.getByTestId("run-now-label")).toHaveTextContent("Its series master has the question");
    expect(screen.getByTestId("run-question-text")).toHaveTextContent("Keep the outbox?");
    expect(screen.getByTestId("run-question-series")).toHaveAttribute("href", expect.stringContaining("sr-1"));
    expect(screen.queryByTestId("pending-question-card")).toBeNull();
  });

  it("RunNow_SeriesLedHold_WithoutSeriesOnTheRead_IsReadOnlyToo", () => {
    const hold = { addressee: "nobody", case: "question", act: { kind: "conversation", target: "sr-1" }, since: null } as unknown as RunTicketHold;
    render(<RunNow runId="r1" snapshot={snap({ status: "waiting_for_input", pendingQuestion: parked })} events={[]} steps={[]}
      ticket={{ ...ticket, hold }} />);
    expect(screen.getByTestId("run-question-series")).toHaveAttribute("href", expect.stringContaining("sr-1"));
  });

  it("RunNow_SeriesOfOneQuestion_KeepsItsAnswerForm", () => {
    const hold = { addressee: "decide", case: "question", act: { kind: "answer", target: null }, since: null } as unknown as RunTicketHold;
    render(<RunNow runId="r1" snapshot={snap({ status: "waiting_for_input", pendingQuestion: parked })} events={[]} steps={[]}
      ticket={{ ...ticket, hold }} />);
    expect(screen.getByTestId("run-now-label")).toHaveTextContent("Waiting for your answer");
    expect(within(screen.getByTestId("run-question")).getByTestId("pending-question-card")).toBeInTheDocument();
  });

  it("RunNow_StoppedNewestAttempt_OffersRetryAndTheTicket", async () => {
    const stopped = snap({ status: "failed", finishedAt: "2026-10-13T07:00:00Z", summary: "The build failed." });
    render(<RunNow runId="r1" snapshot={stopped} status={snapshotStatus(stopped, [], [], ticket)} events={[]} steps={[]} ticket={ticket} />);
    // 2026-11-22-7103a: How it ended opens with the run's status line.
    expect(screen.getByTestId("run-status-line")).toHaveTextContent(/^\d{2}:\d{2}:\d{2}outcomeFailed · The build failed\.$/);
    expect(screen.getByTestId("run-now-ticket")).toHaveAttribute("href", "/tickets/shop/13");
    fireEvent.click(screen.getByTestId("ticket-retry"));
    await waitFor(() => expect(retryAttempt).toHaveBeenCalledWith("r1"));
  });

  it("RunNow_AnOlderAttempt_LinksToTheLatestInsteadOfRetry", () => {
    render(<RunNow runId="r1" snapshot={ended()} events={[]} steps={[]} ticket={{ ...ticket, newest: false, latest: third }} />);
    expect(screen.queryByTestId("ticket-retry")).toBeNull();
    expect(screen.getByTestId("run-now-latest")).toHaveAttribute("href", "/jobs/r3");
    expect(screen.getByTestId("run-now-latest")).toHaveTextContent("attempt 3 is the latest");
    expect(push).not.toHaveBeenCalled();
  });

  // 2026-11-18-d6d2d: a requested Retry follows the new attempt once the ticket's re-read names it.
  it("RunNow_RequestedRetry_OpensTheNewAttemptOnceTheTicketNamesIt", async () => {
    push.mockReset();
    retryAttempt.mockResolvedValueOnce({ kind: "requested" });
    const { rerender } = render(<RunNow runId="r1" snapshot={ended()} events={[]} steps={[]} ticket={ticket} />);
    fireEvent.click(screen.getByTestId("ticket-retry"));
    await waitFor(() => expect(screen.getByTestId("ticket-retry-requested")).toBeInTheDocument());
    expect(push).not.toHaveBeenCalled();
    rerender(<RunNow runId="r1" snapshot={ended()} events={[]} steps={[]} ticket={{ ...ticket, newest: false, latest: third }} />);
    await waitFor(() => expect(push).toHaveBeenCalledWith("/jobs/r3"));
  });

  it("RunNow_RefusedRetry_ReadsTheTicketAgain", async () => {
    reread.mockReset();
    retryAttempt.mockResolvedValueOnce({ kind: "refused", refusal: { reason: "not-latest", text: "attempt r3 is the ticket's latest — only the latest attempt is retried", project: "shop", ticketId: "13" } });
    render(<RunNow runId="r1" snapshot={ended()} events={[]} steps={[]} ticket={ticket} />);
    fireEvent.click(screen.getByTestId("ticket-retry"));
    await waitFor(() => expect(reread).toHaveBeenCalledTimes(1));
    expect(screen.getByTestId("ticket-retry-refused")).toHaveTextContent("only the latest attempt is retried");
  });

  // 2026-11-22-7103a: the status line is How it ended's first line; outcome, why, stopped-at and to-do lines are gone.
  it("RunNow_EndedShortfall_StatusLineReplacesOutcomeWhyStoppedAtAndToDo", () => {
    const short = snap({ status: "shortfall", finishedAt: "2026-10-13T07:00:00Z", prUrl: "https://example.test/server/pull/2",
      summary: "Delivered 1 of 2 spec(s); not delivered: s2 — the agent ended without writing a report." });
    const specs = [
      { specId: "s1", ordinal: 1, title: "one", status: "done", startedAt: null, endedAt: null, verdict: null, decisions: [], steps: [] },
      { specId: "s2", ordinal: 2, title: "two", status: "failed", startedAt: null, endedAt: null, verdict: "The agent ended without writing a report. More.", decisions: [], steps: [] },
    ];
    const failedStep = { stepIndex: 5, stepName: "VerifySpec", displayName: "Build and test", commandName: "VerifySpecCommand", status: "failed", resultMessage: "no report" } as never;
    render(<RunNow runId="r1" snapshot={short} status={snapshotStatus(short, specs, [failedStep], null)} events={[]} steps={[failedStep]} ticket={null} />);
    // The head in the run's colour, the rest in the line's ink (2026-11-22-7103a review).
    expect(screen.getByTestId("run-status-line")).toHaveTextContent(/^\d{2}:\d{2}:\d{2}outcome1 of 2 specs delivered · s2: The agent ended without writing a report\.$/);
    expect(screen.getByTestId("run-status-head")).toHaveTextContent(/^1 of 2 specs delivered$/);
    expect(screen.getByTestId("run-status-head")).toHaveAttribute("data-tone", "bad");
    expect(screen.queryAllByTestId("run-now-line").map((l) => l.getAttribute("data-kind"))).not.toEqual(
      expect.arrayContaining(["outcome", "why", "said", "stopped at", "to do"]));
  });

  it("RunNow_EndedWithSummary_KeepsTheSummaryBehindTheRunsOwnWords", () => {
    const done = snap({ status: "success", finishedAt: "2026-10-13T07:00:00Z", summary: "Merged the retry policy." });
    render(<RunNow runId="r1" snapshot={done} status={snapshotStatus(done, [], [], null)} events={[]} steps={[]} ticket={null} />);
    const said = screen.getByTestId("run-now-said");
    expect(said).not.toHaveAttribute("open");
    expect(said).toHaveTextContent("The run's own words");
    expect(said).toHaveTextContent("Merged the retry policy.");
  });
});
