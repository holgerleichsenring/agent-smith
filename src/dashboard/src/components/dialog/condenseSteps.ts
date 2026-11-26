// 2026-11-12-92e9c: a turn's steps as the few quiet lines a reader follows, the same for the steps pushed while the
// turn ran and for the pages fetched from its run after a reload. Reads and searches in a row are one counted line;
// a write is a line of its own; a model step that narrated something says it; a model step that said nothing is
// dropped, and it does not break a burst of reads, because one sits between every two tool calls.
// 2026-11-13-3dc8: the working line renders these too. A sub-agent's or reviewer's step arrives as "Name › tool": its
// reads and searches join the burst by the tool after the prefix, and its model answers are no lines at all — a
// reviewer's verdict JSON is not the master's narration.
// 2026-11-19-687ea: a model call that STARTED is a thinking step. It is no line of its own — the working view shows the
// last one as a live line while it is the newest step — and, like a silent model step, it does not break a burst.

import type { T } from "@/lib/i18n/translate";

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
const WRITE = /^(write_|edit_|delete_)/; // 2026-11-15-06fc: delete_file changes the draft too
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

/** Whether condenseSteps draws no line for a step: a model call that started, or a model step that said nothing or was a
 *  sub-agent's. */
function dropped(step: RawStep): boolean {
  const name = step.name ?? "";
  return step.kind === "thinking" || (step.kind === "model" && (!step.detail?.trim() || name.includes(PREFIX)));
}

/** 2026-11-26-5201b: the one line a turn's folded steps show — how many steps it took, and of them its reads, searches
 *  and writes ("42 steps · 20 reads · 3 searches · 5 writes"). A step is every step condenseSteps does not drop; the
 *  reads, searches and writes are tool steps by its own name rules, so they never add up to more than the steps. */
export function stepSummary(t: T, steps: RawStep[]): string {
  let total = 0, reads = 0, searches = 0, writes = 0;
  for (const step of steps) {
    if (dropped(step)) continue;
    total += 1;
    if (step.kind !== "tool") continue;
    const tool = toolOf(step.name ?? "");
    if (SEARCH.test(tool)) searches += 1;
    else if (READ.test(tool)) reads += 1;
    else if (WRITE.test(tool)) writes += 1;
  }
  return [
    t("dialog.steps.total", { count: total }),
    reads > 0 ? t("dialog.steps.reads", { count: reads }) : null,
    searches > 0 ? t("dialog.steps.searches", { count: searches }) : null,
    writes > 0 ? t("dialog.steps.writes", { count: writes }) : null,
  ].filter(Boolean).join(" · ");
}

/** The lines of one turn, in the order its steps came. */
export function condenseSteps(t: T, steps: RawStep[]): StepLine[] {
  const lines: StepLine[] = [];
  let burst: Burst | null = null;
  for (const [index, step] of steps.entries()) {
    const name = step.name ?? "";
    if (dropped(step)) continue;
    const tool = toolOf(name);
    if (step.kind === "tool" && (READ.test(tool) || SEARCH.test(tool))) {
      burst ??= { key: step.seq, reads: 0, searches: 0 };
      if (SEARCH.test(tool)) burst.searches += 1;
      else burst.reads += 1;
      continue;
    }
    if (burst) lines.push(burstLine(t, burst));
    burst = null;
    lines.push(lineOf(t, step, name, index < steps.length - 1));
  }
  if (burst) lines.push(burstLine(t, burst));
  return lines;
}

function burstLine(t: T, burst: Burst): StepLine {
  const text = [
    burst.reads > 0 ? t("dialog.steps.reads", { count: burst.reads }) : null,
    burst.searches > 0 ? t("dialog.steps.searches", { count: burst.searches }) : null,
  ].filter(Boolean).join(" · ");
  return { key: burst.key, kind: "reads", text };
}

function lineOf(t: T, step: RawStep, name: string, followed: boolean): StepLine {
  if (step.kind === "model") return { key: step.seq, kind: "said", text: step.detail!.trim() };
  if (step.kind === "revising") return { key: step.seq, kind: "step", text: t("dialog.steps.revising") };
  // 2026-11-20-24bd: the detail is the whole sentence — the model, its limit and the config key.
  // Once anything followed it the wait is over, and the history says so in the past tense. The server's sentence is
  // English (2026-11-22-5485a); only the line this page words itself is translated.
  if (step.kind === "waiting") {
    const said = step.detail?.trim();
    if (!said) return { key: step.seq, kind: "step", text: t(followed ? "dialog.steps.waitedForModel" : "dialog.steps.waitingForModel") };
    return { key: step.seq, kind: "step", text: followed ? said.replace(/^Waiting for/, "Waited for") : said };
  }
  const text = [name || step.kind, step.detail].filter(Boolean).join(" ");
  return { key: step.seq, kind: step.kind === "tool" && WRITE.test(toolOf(name)) ? "write" : "step", text };
}

/** 2026-11-19-687ea: the model call in flight — the newest step when it is a thinking one — with the sub-agent that
 *  made it, or null for the master's own. Null when the newest step is anything else. */
export function inFlight(steps: RawStep[]): { key: number; who: string | null } | null {
  const last = steps.at(-1);
  if (!last || last.kind !== "thinking") return null;
  const name = last.name ?? "";
  const at = name.lastIndexOf(PREFIX);
  return { key: last.seq, who: at < 0 ? null : name.slice(0, at) };
}
