// 2026-11-12-92e9c: a turn's steps as the few quiet lines a reader follows, the same for the steps pushed while the
// turn ran and for the pages fetched from its run after a reload. Reads and searches in a row are one counted line;
// a write is a line of its own; a model step that narrated something says it; a model step that said nothing is
// dropped, and it does not break a burst of reads, because one sits between every two tool calls.
// 2026-11-13-3dc8: the working line renders these too. A sub-agent's or reviewer's step arrives as "Name › tool": its
// reads and searches join the burst by the tool after the prefix, and its model answers are no lines at all — a
// reviewer's verdict JSON is not the master's narration.

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
const PREFIX = " › ";

/** The tool a step called, after the name of the sub-agent that called it. */
function toolOf(name: string): string {
  const at = name.lastIndexOf(PREFIX);
  return at < 0 ? name : name.slice(at + PREFIX.length);
}

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
    if (step.kind === "model" && (!step.detail?.trim() || name.includes(PREFIX))) continue;
    const tool = toolOf(name);
    if (step.kind === "tool" && (READ.test(tool) || SEARCH.test(tool))) {
      burst ??= { key: step.seq, reads: 0, searches: 0 };
      if (SEARCH.test(tool)) burst.searches += 1;
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
  return { key: step.seq, kind: step.kind === "tool" && WRITE.test(toolOf(name)) ? "write" : "step", text };
}
