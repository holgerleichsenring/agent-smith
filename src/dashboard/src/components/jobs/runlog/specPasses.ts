import { passEndWords } from "@/components/tickets/detail/flowWords";
import type { T } from "@/lib/i18n/translate";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import { commandKey } from "./runSections";

// 2026-11-24-0fbfa: which master pass of its spec a master step is. A spec's later pass — the structure correction, a
// repair, the pass after an answer, a review fix — is a master step the spec's earlier step queued, so it is read off
// that step: what kind of pass (its command), and why (its result message). Once the pass has ended, its recorded
// MasterPassEnded says what it opened and ended because, which outranks the reading of the command.

/** The opener word (MasterPassReasons) of a pass queued by this command. */
const OPENED_BY: Record<string, string> = {
  CommitSpecWork: "structure-correction",
  VerifySpec: "repair",
  RunVerifyPhase: "repair",
  MasterOpenQuestions: "after-answer",
  ReviewSpecDiff: "review-fix",
};

export interface StepPass {
  /** 1-based, among the spec's master steps. */
  number: number;
  /** MasterPassReasons opener word; null for the spec's first pass, or a later one queued by a step it does not know. */
  opened: string | null;
  /** The queuing step's result message: why the pass runs. Null for the first pass. */
  because: string | null;
  /** How the step's last recorded pass ended; null while it runs or when none was recorded. */
  ended: string | null;
}

function isMaster(row: RunStepRow): boolean {
  return commandKey(row) === "AgenticMaster";
}

/** The newest recorded pass end per step index. */
function passEnds(events: RunEvent[]): Map<number, { opened: string; ended: string }> {
  const ends = new Map<number, { opened: string; ended: string }>();
  for (const e of events) {
    if (e.type !== EventType.MasterPassEnded || e.originStepIndex == null) continue;
    const first = ends.get(e.originStepIndex)?.opened;
    ends.set(e.originStepIndex, { opened: first ?? e.openedBecause, ended: e.endedBecause });
  }
  return ends;
}

/** The master steps of one spec's rows that are passes of a spec run more than once, by step index; empty when the
 *  spec ran its master once — a single pass needs no number. */
export function specPasses(rows: RunStepRow[], events: RunEvent[]): Map<number, StepPass> {
  const sorted = [...rows].sort((l, r) => l.stepIndex - r.stepIndex);
  const masters = sorted.filter(isMaster);
  const out = new Map<number, StepPass>();
  if (masters.length < 2) return out;
  const ends = passEnds(events);
  masters.forEach((row, i) => {
    const before = i === 0 ? null : sorted[sorted.indexOf(row) - 1] ?? null;
    const recorded = ends.get(row.stepIndex);
    const opened = i === 0 ? null : recorded?.opened ?? (before ? OPENED_BY[commandKey(before)] ?? null : null);
    out.set(row.stepIndex, { number: i + 1, opened: opened === "first" ? null : opened, because: i === 0 ? null : before?.resultMessage ?? null, ended: recorded?.ended ?? null });
  });
  return out;
}

/** "Pass 2 of this spec · structure correction", "Pass 1 of this spec · ended: nothing left to do". */
export function passHead(t: T, pass: StepPass): string {
  const parts = [t("runs.log.pass.number", { number: pass.number })];
  if (pass.number > 1) parts.push(pass.opened ? passEndWords(t, pass.opened) : t("runs.log.pass.another"));
  if (pass.ended) parts.push(t("runs.log.pass.ended", { reason: passEndWords(t, pass.ended) }));
  return parts.join(" · ");
}
