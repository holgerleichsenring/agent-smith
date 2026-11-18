import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { TicketAttempt } from "@/types/tickets";

// 2026-11-17-6d9ea: the Now block follows the agent from the hub's events, asks the question while waiting, and once
// the run has ended says how, with its acts: pull requests, Retry unchanged, the ticket, why.

vi.mock("@/hooks/useSandboxActivity", () => ({ useSandboxActivity: () => null }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => false }));
const retryAttempt = vi.fn().mockResolvedValue({ kind: "requested" });
vi.mock("@/lib/ticketsApi", async (original) => ({ ...(await original<object>()), retryAttempt: (id: string) => retryAttempt(id) }));

const { RunNow } = await import("../RunNow");

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
const ticket = { label: "GH-13", href: "/tickets/shop/13", project: "shop", number: 2, of: 2, attempt, newest: true };

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

  it("RunNow_StoppedNewestAttempt_OffersRetryAndTheTicket", async () => {
    render(<RunNow runId="r1" snapshot={snap({ status: "failed", finishedAt: "2026-10-13T07:00:00Z", summary: "The build failed." })}
      events={[]} steps={[]} ticket={ticket} />);
    expect(screen.getAllByTestId("run-now-line")[0]).toHaveTextContent("Stopped · no pull request opened");
    expect(screen.getByTestId("run-now-ticket")).toHaveAttribute("href", "/tickets/shop/13");
    fireEvent.click(screen.getByTestId("ticket-retry"));
    await waitFor(() => expect(retryAttempt).toHaveBeenCalledWith("r1"));
  });

  it("RunNow_AnOlderAttempt_OffersNoRetry", () => {
    render(<RunNow runId="r1" snapshot={snap({ status: "failed", finishedAt: "2026-10-13T07:00:00Z" })}
      events={[]} steps={[]} ticket={{ ...ticket, newest: false }} />);
    expect(screen.queryByTestId("ticket-retry")).toBeNull();
  });
});
