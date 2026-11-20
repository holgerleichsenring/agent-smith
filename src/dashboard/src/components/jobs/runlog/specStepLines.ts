import type { RunSpecRow } from "@/lib/runSpecsApi";
import type { ProgressLedgerEntry } from "@/types/hub-events";
import type { RunPassStepMoved } from "@/types/tickets";
import type { SpecStep } from "./specRecord";

// 2026-11-19-1b72d: the steps of the spec an opened step works on, each done, in progress or open — the list behind
// "N of M steps done", which was counted and never shown. The progress ledger is seeded 1:1 from the spec's steps
// (ProgressLedgerSeeder, ids are the step ids), so while a spec is the run's newest it IS that list. An earlier spec's
// ledger was replaced by the next one's seed: its steps come from the record it executed, their state from the
// ledger transitions the run recorded for that spec (RunPassesView), the last state each step was moved to.

export type SpecStepState = "done" | "in_progress" | "open";

export interface SpecStepLine {
  id: string;
  /** 1-based, in the spec's order. */
  number: number;
  text: string;
  state: SpecStepState;
}

function stateOf(status: string | null | undefined): SpecStepState {
  switch ((status ?? "").toLowerCase().replace(/[^a-z]/g, "")) {
    case "done": return "done";
    case "inprogress": return "in_progress";
    default: return "open";
  }
}

/** The ledger as step lines. */
export function stepLinesFromLedger(ledger: ProgressLedgerEntry[]): SpecStepLine[] {
  return ledger.map((e, i) => ({ id: e.id, number: i + 1, text: e.activity, state: stateOf(e.status) }));
}

/** The record's steps, each in the state the last transition of its id moved it to; a step never moved is open. */
export function stepLinesFromRecord(steps: SpecStep[], moved: RunPassStepMoved[]): SpecStepLine[] {
  const last = new Map<string, string | null>();
  for (const m of moved) last.set(m.entryId, m.to);
  return steps.map((s, i) => ({ id: s.id, number: i + 1, text: s.action, state: stateOf(last.get(s.id)) }));
}

/** The spec the run's ledger belongs to: the newest one that started, or the only one the run names. */
export function ledgerSpec(specs: RunSpecRow[]): string | null {
  const started = specs.filter((s) => s.startedAt !== null).sort((l, r) => (l.startedAt ?? "").localeCompare(r.startedAt ?? ""));
  return started.at(-1)?.specId ?? (specs.length === 1 ? specs[0].specId : null);
}

/** "2 of 6 done". */
export function specStepsCount(lines: SpecStepLine[]): string {
  return `${lines.filter((l) => l.state === "done").length} of ${lines.length} done`;
}

export const SPEC_STEP_WORDS: Record<SpecStepState, string> = { done: "done", in_progress: "in progress", open: "open" };
