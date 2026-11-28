import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { translator } from "@/lib/i18n/translate";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runStatusWord } from "../runStatus";
import { RunStepHead } from "../runlog/RunStepHead";
import { runStatus, statusSentence } from "../runlog/runStatusLine";

// 2026-11-23-5669c: the run's words in German — the status line, the status word and the step head read from the
// dictionary.

describe("Runs in German", () => {
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
