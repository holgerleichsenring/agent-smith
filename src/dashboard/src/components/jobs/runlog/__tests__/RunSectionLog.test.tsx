import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { RunProgressStrip, RunSectionLog } from "../RunSectionLog";
import { runSections, type SectionId } from "../runSections";
import { RunStatusBox, runStatusWords } from "../RunStatusBox";

// 2026-11-16-6877b: the sectioned log as a person meets it — every section closed, a strip node opens its section,
// the check section hosts what it is given; and the status sentence for every state.

const steps = [
  { stepIndex: 1, stepName: "FetchTicket", displayName: "Fetch ticket", commandName: "FetchTicket", status: "success", durationSeconds: 28,
    resultMessage: "GH-13 fetched", llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0 },
  { stepIndex: 2, stepName: "ReviewSpecDiff", displayName: "Review the spec's diff", commandName: "ReviewSpecDiff", status: "success",
    durationSeconds: 170, resultMessage: "1 finding left open", hasFinding: true, llmCalls: 3, costUsd: 0.1, sandboxCommands: 0, subAgents: 0 },
];

function Log() {
  const [open, setOpen] = useState<ReadonlySet<SectionId>>(new Set());
  const toggle = (id: SectionId) => setOpen((now) => {
    const next = new Set(now);
    if (!next.delete(id)) next.add(id);
    return next;
  });
  const sections = runSections(steps, [], "done", "2 of 2 done criteria met");
  return (
    <>
      <RunProgressStrip sections={sections} open={open} onToggle={toggle} />
      <RunSectionLog sections={sections} open={open} onToggle={toggle} check={<div data-testid="criteria-here">criteria</div>} />
    </>
  );
}

describe("RunSectionLog", () => {
  it("RunSectionLog_EverySectionStartsClosed_WithTitleSummaryAndTime", () => {
    render(<Log />);
    expect(screen.getAllByRole("button", { expanded: false })).toHaveLength(7);
    expect(screen.queryAllByTestId("run-log-line")).toHaveLength(0);
    const ready = screen.getByTestId("run-section-ready");
    expect(ready).toHaveTextContent("Got ready");
    expect(ready).toHaveTextContent("GH-13 fetched");
    expect(ready).toHaveTextContent("28 s");
    expect(screen.getByTestId("run-section-review")).toHaveAttribute("data-state", "warn");
    expect(screen.getByTestId("run-section-review")).toHaveTextContent("2 min 50 s");
  });

  it("RunProgressStrip_ANodeOpensItsSection_AndTheHeaderClosesIt", () => {
    render(<Log />);
    expect(screen.getByTestId("run-node-review")).toHaveClass("warn");
    expect(screen.getByTestId("run-node-deliver")).toHaveClass("pending");
    fireEvent.click(screen.getByTestId("run-node-review"));
    expect(screen.getByTestId("run-node-review")).toHaveAttribute("aria-pressed", "true");
    const lines = within(screen.getByTestId("run-section-review")).getAllByTestId("run-log-line");
    expect(lines[0]).toHaveTextContent("Review the spec's diff: 1 finding left open");
    fireEvent.click(within(screen.getByTestId("run-section-review")).getByRole("button", { expanded: true }));
    expect(screen.queryAllByTestId("run-log-line")).toHaveLength(0);
  });

  it("RunSectionLog_CheckSection_HostsTheCriteria", () => {
    render(<Log />);
    fireEvent.click(screen.getByTestId("run-node-check"));
    expect(within(screen.getByTestId("run-section-check")).getByTestId("criteria-here")).toBeInTheDocument();
  });
});

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:05:27Z", finishedAt: null, sandboxes: 1, stepIndex: 2, stepName: "Build and test",
    totalSteps: 5, lastEventType: null, costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false, ...over,
  };
}

describe("RunStatusBox", () => {
  it("runStatusWords_SaysOneSentencePerState", () => {
    expect(runStatusWords(snap())).toEqual({ tone: "run", text: "Working: Build and test. Nothing for you to do." });
    expect(runStatusWords(snap({ status: "queued", queuePosition: 2 }))).toEqual({ tone: "run", text: "Waiting for capacity, place 2 in line." });
    expect(runStatusWords(snap({ status: "waiting_for_input" }))).toEqual({ tone: "amber", text: "Waiting for your answer." });
    expect(runStatusWords(snap({ status: "success", summary: "Delivered to a branch." }))).toEqual({ tone: "ok", text: "Delivered to a branch." });
    expect(runStatusWords(snap({ status: "success" })).text).toBe("Delivered.");
    expect(runStatusWords(snap({ status: "failed", summary: "Token cap reached." }))).toEqual({ tone: "bad", text: "Token cap reached." });
    expect(runStatusWords(snap({ status: "cancelled" }))).toEqual({ tone: "quiet", text: "Cancelled." });
  });

  it("RunStatusBox_AWaitingRun_ShowsTheQuestionCard_AFinishedOne_ItsPullRequests", () => {
    const question = { questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null,
      askedAt: "2026-10-13T06:10:00Z", answerDeadlineAt: "2026-10-14T06:10:00Z" };
    const { rerender } = render(<RunStatusBox snapshot={snap({ status: "waiting_for_input", pendingQuestion: question })} />);
    expect(within(screen.getByTestId("run-question")).getByTestId("pending-question-card")).toHaveTextContent("Keep the outbox?");
    rerender(<RunStatusBox snapshot={snap({ status: "success", pullRequests: [{ repo: "server", url: "https://example.test/pr/1", status: "opened", isDraft: false }] })} />);
    expect(screen.queryByTestId("run-question")).toBeNull();
    expect(screen.getByTestId("run-status-pr")).toHaveAttribute("href", "https://example.test/pr/1");
  });
});
