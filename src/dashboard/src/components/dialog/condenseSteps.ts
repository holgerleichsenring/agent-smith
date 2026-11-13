// 2026-11-12-92e9c: a turn's steps as the few quiet lines a reader follows, the same for the steps pushed while the
// turn ran and for the pages fetched from its run after a reload. Reads and searches in a row are one counted line;
// a write is a line of its own; a model step that narrated something says it; a model step that said nothing is
// dropped, and it does not break a burst of reads, because one sits between every two tool calls.

/** What both a pushed step and a fetched one carry. */
export interface RawStep {
  kind: string;
  name?: string | null;
  detail?: string | null;
  seq: number;
}

export type StepLineKind = "reads" | "write" | "said" | "step";

export interface StepLine {
  /** The sequence of the first step the line stands for. */
  key: number;
  kind: StepLineKind;
  text: string;
}

const SEARCH = /^(grep|search|find_)/;
const READ = /^(read_|list_)/;
const WRITE = /^(write_|edit_)/;

interface Burst {
  key: number;
  reads: number;
  searches: number;
}

/** The lines of one turn, in the order its steps came. */
export function condenseSteps(steps: RawStep[]): StepLine[] {
  const lines: StepLine[] = [];
  let burst: Burst | null = null;
  for (const step of steps) {
    const name = step.name ?? "";
    if (step.kind === "model" && !step.detail?.trim()) continue;
    if (step.kind === "tool" && (READ.test(name) || SEARCH.test(name))) {
      burst ??= { key: step.seq, reads: 0, searches: 0 };
      if (SEARCH.test(name)) burst.searches += 1;
      else burst.reads += 1;
      continue;
    }
    if (burst) lines.push(burstLine(burst));
    burst = null;
    lines.push(lineOf(step, name));
  }
  if (burst) lines.push(burstLine(burst));
  return lines;
}

function burstLine(burst: Burst): StepLine {
  const text = [
    burst.reads > 0 ? `${burst.reads} ${burst.reads === 1 ? "read" : "reads"}` : null,
    burst.searches > 0 ? `${burst.searches} ${burst.searches === 1 ? "search" : "searches"}` : null,
  ].filter(Boolean).join(" · ");
  return { key: burst.key, kind: "reads", text };
}

function lineOf(step: RawStep, name: string): StepLine {
  if (step.kind === "model") return { key: step.seq, kind: "said", text: step.detail!.trim() };
  if (step.kind === "reviewing") return { key: step.seq, kind: "step", text: "reviewing its proposal" };
  if (step.kind === "revising") return { key: step.seq, kind: "step", text: "revising its answer" };
  const text = [name || step.kind, step.detail].filter(Boolean).join(" ");
  return { key: step.seq, kind: step.kind === "tool" && WRITE.test(name) ? "write" : "step", text };
}
