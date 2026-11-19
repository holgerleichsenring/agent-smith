import { describe, expect, it } from "vitest";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import { ledgerSpec, specStepsCount, stepLinesFromLedger, stepLinesFromRecord } from "../specStepLines";

// 2026-11-19-1b72d: the steps of a spec with their state — from the run's ledger for the spec it belongs to, else from
// the executed record and the run's recorded transitions.

const spec = (specId: string, startedAt: string | null): RunSpecRow =>
  ({ specId, ordinal: 0, title: specId, status: "done", startedAt, endedAt: null, verdict: null, decisions: [], steps: [] });

describe("specStepLines", () => {
  it("stepLinesFromLedger_NumbersTheEntries_AndReadsTheirState", () => {
    const lines = stepLinesFromLedger([
      { id: "a", activity: "first", status: "done", target: null }, { id: "b", activity: "second", status: "in_progress", target: null },
      { id: "c", activity: "third", status: "pending", target: null },
    ]);
    expect(lines.map((l) => [l.number, l.text, l.state])).toEqual([[1, "first", "done"], [2, "second", "in_progress"], [3, "third", "open"]]);
    expect(specStepsCount(lines)).toBe("1 of 3 done");
  });

  it("stepLinesFromRecord_TheLastTransitionWins_AndAStepNeverMovedIsOpen", () => {
    const moved = (entryId: string, to: string) => ({ entryId, activity: "", from: null, to, cause: "model_update", pass: 1 });
    const lines = stepLinesFromRecord([{ id: "a", action: "first" }, { id: "b", action: "second" }, { id: "c", action: "third" }],
      [moved("a", "in_progress"), moved("a", "done"), moved("b", "InProgress"), moved("x", "done")]);
    expect(lines.map((l) => l.state)).toEqual(["done", "in_progress", "open"]);
  });

  it("ledgerSpec_IsTheNewestStartedSpec", () => {
    expect(ledgerSpec([spec("one", "2026-11-19T10:00:00Z"), spec("two", "2026-11-19T11:00:00Z"), spec("three", null)])).toBe("two");
    expect(ledgerSpec([spec("only", null)])).toBe("only");
    expect(ledgerSpec([])).toBeNull();
  });
});
