import type { RunSpecRow } from "@/lib/runSpecsApi";
import type { RunStepRow } from "@/lib/runStepsApi";
import { isStoryRow, splitSpecPrefix } from "@/lib/runStepRail";
import type { ProgressLedgerEntry, RunSnapshot } from "@/types/hub-events";
import { checkOf, commandSection, sectionState, stepState, type SectionState } from "./runSections";
import { pullRequestLinks } from "./runNowLines";
import { spanWords } from "./runMetaWords";

// 2026-11-17-6d9eb: the run log sectioned by work, as the approved mock draws it. Every step row the server projects
// lands in exactly one section: before the first spec step in Got ready, a spec's steps in that spec's section, a later
// step without a spec in Delivered when its command delivers and otherwise in the section before it. A run that cut no
// spec reads Got ready, The work, Delivered by command. Ticket, The plan and Checked against the spec carry what the run
// read and was judged on rather than steps. Each section and step says its llm calls, cost and time.

export type LogSectionKind = "ticket" | "ready" | "plan" | "spec" | "work" | "check" | "deliver";

export interface LogStep {
  row: RunStepRow;
  label: string;
  state: SectionState;
  /** False for an internal step or a silent gate: it sits behind the section's "n mechanics steps" line. */
  story: boolean;
}

export interface LogSection {
  id: string;
  kind: LogSectionKind;
  title: string;
  specId: string | null;
  state: SectionState;
  meta: string;
  steps: LogStep[];
}

const WIRE_STATUS: Record<string, "ok" | "fail" | "run" | "cancel" | "wait"> = {
  success: "ok", failed: "fail", error: "fail", running: "run", cancelled: "cancel",
};

function toStep(row: RunStepRow): LogStep {
  const status = row.planned ? "wait" : WIRE_STATUS[row.status ?? ""] ?? "wait";
  return {
    row,
    label: splitSpecPrefix(row.displayName ?? row.stepName).label,
    state: row.planned ? "pending" : stepState(row),
    story: isStoryRow({ status, stepClass: row.stepClass, hasFinding: row.hasFinding }),
  };
}

function specOf(row: RunStepRow): string | null {
  return row.specId ?? splitSpecPrefix(row.displayName ?? row.stepName).specId;
}

/** Which section each step row belongs to, in step order. */
export function placeLogSteps(steps: RunStepRow[]): Array<{ row: RunStepRow; key: string }> {
  const sorted = [...steps].sort((l, r) => l.stepIndex - r.stepIndex);
  const anySpec = sorted.some((r) => specOf(r) !== null);
  const out: Array<{ row: RunStepRow; key: string }> = [];
  let previous = "ready";
  let seenSpec = false;
  for (const row of sorted) {
    const spec = specOf(row);
    const command = commandSection(row);
    let key: string;
    if (spec) {
      key = `spec:${spec}`;
      seenSpec = true;
    } else if (anySpec) {
      key = !seenSpec ? "ready" : command === "deliver" || previous === "deliver" ? "deliver" : previous;
    } else if (command === "deliver" || previous === "deliver") {
      key = "deliver";
    } else if (command === "ready" || command === "spec") {
      key = previous === "work" ? "work" : "ready";
    } else {
      key = command ? "work" : previous;
    }
    out.push({ row, key });
    previous = key;
  }
  return out;
}

/** "1.5s", "4m 26s", "75m 10s"; empty for no time. */
export function stepTime(seconds: number | null | undefined): string {
  if (!seconds || seconds <= 0) return "";
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const m = Math.floor(seconds / 60);
  return `${m}m ${Math.round(seconds - m * 60)}s`;
}

export function llmWords(calls: number | null | undefined): string {
  return calls ? `${calls} llm` : "";
}

export function costWords(usd: number | null | undefined): string {
  return usd && usd > 0 ? `$${usd.toFixed(2)}` : "";
}

/** "7 steps · 88 llm · $0.08 · 4m 52s", or "3 of 5 steps done · …" while some are not. */
export function stepsMeta(rows: RunStepRow[]): string {
  const reached = rows.filter((r) => !r.planned && r.status);
  const done = reached.filter((r) => r.status === "success").length;
  const parts = [done === rows.length ? (rows.length === 1 ? "1 step" : `${rows.length} steps`) : `${done} of ${rows.length} steps done`];
  const llm = rows.reduce((n, r) => n + (r.llmCalls ?? 0), 0);
  const cost = rows.reduce((n, r) => n + (r.costUsd ?? 0), 0);
  const seconds = rows.reduce((n, r) => n + (r.durationSeconds ?? 0), 0);
  if (llm > 0) parts.push(llmWords(llm));
  if (cost > 0) parts.push(costWords(cost));
  const span = spanWords(seconds);
  if (span) parts.push(span);
  return parts.join(" · ");
}

const SPEC_STATE: Record<string, SectionState | undefined> = { failed: "bad", handed_back: "bad" };

function ledgerMeta(ledger: ProgressLedgerEntry[] | null): { state: SectionState; meta: string } {
  if (!ledger || ledger.length === 0) return { state: "pending", meta: "the work spec and the agreed plan" };
  const done = ledger.filter((e) => e.status === "done").length;
  const state: SectionState = done === ledger.length ? "done" : ledger.some((e) => e.status === "in_progress") ? "running" : "pending";
  return { state, meta: `${done} of ${ledger.length} plan steps done` };
}

function deliverMeta(snapshot: RunSnapshot | null, rows: RunStepRow[]): string {
  const status = (snapshot?.status ?? "").toLowerCase();
  const prs = snapshot ? pullRequestLinks(snapshot).length : 0;
  const finished = status === "success" || status === "shortfall" || status === "failed" || status === "error" || status === "cancelled";
  const words = prs === 1 ? "pull request opened" : prs > 1 ? `${prs} pull requests opened` : finished ? "no pull request opened" : "not reached yet";
  const steps = rows.length > 0 ? stepsMeta(rows) : "";
  return steps ? `${words} · ${steps}` : words;
}

export function runLogSections(
  steps: RunStepRow[], specs: RunSpecRow[], snapshot: RunSnapshot | null, ledger: ProgressLedgerEntry[] | null,
): LogSection[] {
  const placed = placeLogSteps(steps);
  const rowsOf = (key: string) => placed.filter((p) => p.key === key).map((p) => p.row);
  const stepSection = (key: string, kind: LogSectionKind, title: string, specId: string | null = null): LogSection => {
    const rows = rowsOf(key);
    return { id: key, kind, title, specId, state: sectionState(rows), meta: rows.length > 0 ? stepsMeta(rows) : "nothing ran here", steps: rows.map(toStep) };
  };

  const sections: LogSection[] = [];
  // 2026-11-18-e06ba: the head names the ticket by id; its title opens the section's body, where it has the room.
  const ticketId = snapshot?.ticketId ?? null;
  sections.push({ id: "ticket", kind: "ticket", title: "Ticket", specId: null, state: ticketId || snapshot?.ticketTitle ? "done" : "pending",
    meta: ticketId ?? "the ticket as the run read it", steps: [] });
  sections.push(stepSection("ready", "ready", "Got ready"));
  const plan = ledgerMeta(ledger);
  sections.push({ id: "plan", kind: "plan", title: "The plan", specId: null, state: plan.state, meta: plan.meta, steps: [] });

  const specKeys = [...new Set(placed.map((p) => p.key).filter((k) => k.startsWith("spec:")))];
  for (const spec of specs) if (!specKeys.includes(`spec:${spec.specId}`)) specKeys.push(`spec:${spec.specId}`);
  for (const key of specKeys) {
    const specId = key.slice("spec:".length);
    const row = specs.find((s) => s.specId === specId);
    const section = stepSection(key, "spec", row?.title ? `Spec ${specId} · ${row.title}` : `Spec ${specId}`, specId);
    const forced = row ? SPEC_STATE[row.status] : undefined;
    if (forced) section.state = forced;
    if (row?.status === "handed_back") section.meta = `handed back · ${section.meta}`;
    if (row?.verdict) section.meta = `${section.meta} · ${row.verdict}`;
    sections.push(section);
  }
  if (rowsOf("work").length > 0) sections.push(stepSection("work", "work", "The work"));

  const check = checkOf(snapshot?.acceptance);
  const unmeasured = snapshot?.acceptance?.unmeasured?.length ?? 0;
  sections.push({ id: "check", kind: "check", title: "Checked against the spec", specId: null,
    state: unmeasured > 0 ? "bad" : check.state, meta: unmeasured > 0 ? `${check.summary} · not measured` : check.summary, steps: [] });

  const deliver = stepSection("deliver", "deliver", "Delivered");
  deliver.meta = deliverMeta(snapshot, rowsOf("deliver"));
  sections.push(deliver);
  return sections;
}

/** The sections a person meets open: one that is running or stopped. */
export function initiallyOpen(sections: LogSection[]): Set<string> {
  const open = sections.filter((s) => s.steps.length > 0 && (s.state === "running" || s.state === "bad")).map((s) => s.id);
  return new Set(open);
}
