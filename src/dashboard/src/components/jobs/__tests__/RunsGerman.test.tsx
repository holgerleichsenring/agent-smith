import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { translator } from "@/lib/i18n/translate";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";
import { RunCard } from "../RunCard";
import { runStatusWord } from "../runStatus";
import { RunStepHead } from "../runlog/RunStepHead";
import { runStatus, statusSentence } from "../runlog/runStatusLine";

// 2026-11-23-5669c: the run card in German — the status word, the step and the cancel control read from the
// dictionary; the pipeline and the repositories are shown as the run named them.

const snapshot: RunSnapshot = {
  runId: "2026-05-27T10-00-00-aaaa",
  pipeline: "fix-bug",
  trigger: "ticket",
  repos: ["api", "worker"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: new Date(Date.now() - 30_000).toISOString(),
  finishedAt: null,
  sandboxes: 1,
  stepIndex: 3,
  stepName: "AnalyzeCode",
  totalSteps: 10,
  lastEventType: "StepStarted",
  costUsd: 0,
  llmCalls: 0,
  ticketId: null,
  ticketTitle: null,
  agentName: null,
  cancelRequested: false,
};

describe("Runs in German", () => {
  it("RunCard_German_WordsStatusStepAndCancel", () => {
    resetLanguage("de");
    render(<RunCard snapshot={snapshot} />);
    expect(screen.getByText("läuft")).toBeInTheDocument();
    expect(screen.getByText("Schritt 3/10")).toBeInTheDocument();
    expect(screen.getByText("2 Repos")).toBeInTheDocument();
    expect(screen.getByTestId(`cancel-run-${snapshot.runId}`)).toHaveTextContent("abbrechen");
    expect(screen.getByTestId(`cancel-run-${snapshot.runId}`)).toHaveAttribute("title", "Diesen Run abbrechen");
    expect(screen.getByText("fix-bug")).toBeInTheDocument();
  });

  it("RunStatusLine_German_WordsTheHeadAndClauses_KeepsTheVerdict", () => {
    const de = translator("de");
    const line = runStatus(de, {
      status: "shortfall", pullRequests: ["api"],
      specs: [{ specId: "a1", ordinal: 1, status: "done", verdict: null }, { specId: "b2", ordinal: 2, status: "failed", verdict: null }],
    });
    expect(statusSentence(line)).toBe("1 von 2 Specs geliefert · b2 nicht geliefert.");
    expect(runStatus(de, { status: "failed", specs: [], pullRequests: [], summary: "Checkout failed: the branch is gone." }).head)
      .toBe("Fehlgeschlagen");
    expect(runStatusWord(de, "queued")).toBe("in der Warteschlange – wartet auf Kapazität");
  });

  it("RunStepHead_German_WordsTheStepsStateAndCounts", () => {
    resetLanguage("de");
    const row: RunStepRow = {
      stepIndex: 2, stepName: "AgenticMaster", displayName: "Run master skill", commandName: "AgenticMaster", status: "success",
      durationSeconds: null, resultMessage: "Pull request created", llmCalls: 0, costUsd: 0, sandboxCommands: 3, subAgents: 1,
    };
    render(<RunStepHead step={{ row, label: "Run master skill", state: "done", story: true }} />);
    expect(screen.getByTestId("step-head")).toHaveTextContent("fertig · 3 Sandbox-Befehle · 1 Sub-Agent");
    expect(screen.getByTestId("step-head-message")).toHaveTextContent("Pull request created");
  });
});
