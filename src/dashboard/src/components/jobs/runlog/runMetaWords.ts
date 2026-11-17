import { isRelaunching, relaunchPlace } from "@/components/jobs/runStatus";
import type { RunSnapshot } from "@/types/hub-events";
import { budgetShare, computeOf, isTerminal, timeSplitWords } from "./runFactWords";

// 2026-11-17-6d9ea: the run page's one meta line and the details behind it. The meta line says the state, the steps,
// the time, the cost against its cap, the compute and the shape; every other fact the run recorded is a details line.

export type StateTone = "ok" | "run" | "amber" | "bad" | "quiet";

/** The state word the meta line starts with, and its colour. */
export function stateWords(snapshot: RunSnapshot): { text: string; tone: StateTone } {
  const status = (snapshot.status ?? "").toLowerCase();
  if (isRelaunching(snapshot)) {
    const place = relaunchPlace(snapshot);
    return { text: place != null ? `resuming, place ${place}` : "resuming", tone: "run" };
  }
  switch (status) {
    case "running": return { text: "running", tone: "run" };
    case "queued": return { text: snapshot.queuePosition ? `queued, place ${snapshot.queuePosition}` : "queued", tone: "run" };
    case "waiting_for_input": return { text: "needs you", tone: "amber" };
    case "success": return { text: "done", tone: "ok" };
    case "shortfall": return { text: "done, with a shortfall", tone: "amber" };
    case "cancelled": return { text: "cancelled", tone: "quiet" };
    case "failed": case "error": return { text: "failed", tone: "bad" };
    case "": return { text: "reading", tone: "quiet" };
    default: return { text: status.replace(/_/g, " "), tone: "bad" };
  }
}

/** "43s", "12m 05s", "1h 18m"; null for no time. */
export function spanWords(seconds: number): string | null {
  if (!Number.isFinite(seconds)) return null;
  const s = Math.max(0, Math.round(seconds));
  if (s <= 0) return null;
  if (s < 60) return `${s}s`;
  if (s < 3600) return `${Math.floor(s / 60)}m ${String(s % 60).padStart(2, "0")}s`;
  return `${Math.floor(s / 3600)}h ${String(Math.floor((s % 3600) / 60)).padStart(2, "0")}m`;
}

/** "1h 18m so far" on a live run, "1h 36m" on a finished one. */
export function elapsedWords(snapshot: RunSnapshot, nowMs: number = Date.now()): string | null {
  const start = Date.parse(snapshot.startedAt);
  if (Number.isNaN(start)) return null;
  const end = snapshot.finishedAt ? Date.parse(snapshot.finishedAt) : nowMs;
  const span = spanWords((end - start) / 1000);
  if (!span) return null;
  return snapshot.finishedAt || isTerminal(snapshot) ? span : `${span} so far`;
}

export interface MetaPart {
  key: string;
  text: string;
  tone?: StateTone | "near" | "over";
}

/** The meta line: state · steps · elapsed · cost of cap · pods · mem · shape, each only when recorded. */
export function metaParts(snapshot: RunSnapshot, stepCount: number, nowMs: number = Date.now()): MetaPart[] {
  const state = stateWords(snapshot);
  const parts: MetaPart[] = [{ key: "state", text: state.text, tone: state.tone }];
  if (!isTerminal(snapshot) && snapshot.totalSteps > 0) parts.push({ key: "steps", text: `step ${snapshot.stepIndex} of ${snapshot.totalSteps}` });
  else if (stepCount > 0) parts.push({ key: "steps", text: stepCount === 1 ? "1 step" : `${stepCount} steps` });
  const elapsed = elapsedWords(snapshot, nowMs);
  if (elapsed) parts.push({ key: "elapsed", text: elapsed });
  const cap = snapshot.budgetCapUsd;
  if (cap != null && cap > 0) {
    const budget = budgetShare(snapshot);
    parts.push({ key: "cost", text: `$${snapshot.costUsd.toFixed(2)} of $${cap.toFixed(2)}`, tone: budget && budget.tone !== "ok" ? budget.tone : undefined });
  } else if (snapshot.costUsd > 0) {
    parts.push({ key: "cost", text: `$${snapshot.costUsd.toFixed(2)}` });
  }
  const pods = snapshot.liveCompute?.pods ?? [];
  if (pods.length > 0) {
    parts.push({ key: "pods", text: pods.length === 1 ? "1 pod" : `${pods.length} pods` });
    if (snapshot.liveCompute?.totalMem) parts.push({ key: "mem", text: snapshot.liveCompute.totalMem });
  } else if (computeOf(snapshot)) {
    parts.push({ key: "pods", text: "compute calculating" });
  }
  if (snapshot.workShape) parts.push({ key: "shape", text: `shape ${snapshot.workShape}` });
  return parts;
}

export interface SandboxBeat {
  commands: number;
  lastCommand?: string | null;
}

/** Everything else the run recorded, one line each, for the details under the meta line. */
export function detailLines(snapshot: RunSnapshot, repoNames: string[], sandbox: SandboxBeat | null): string[] {
  const lines: string[] = [];
  const at = (iso: string) => new Date(iso).toLocaleString("en-GB", {
    day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false,
  });
  if (!Number.isNaN(Date.parse(snapshot.startedAt))) {
    lines.push(snapshot.finishedAt ? `started ${at(snapshot.startedAt)}, finished ${at(snapshot.finishedAt)}` : `started ${at(snapshot.startedAt)}`);
  }
  if (snapshot.agentName || snapshot.llmCalls > 0) {
    const calls = snapshot.llmCalls === 1 ? "1 model call" : `${snapshot.llmCalls} model calls`;
    lines.push(snapshot.agentName ? `model ${snapshot.agentName}, ${calls}` : calls);
  }
  if (repoNames.length > 0) lines.push(`${repoNames.length === 1 ? "repository" : "repositories"} ${repoNames.join(", ")}`);
  if (snapshot.pipeline) lines.push(`pipeline ${snapshot.pipeline}`);
  lines.push(`run ${snapshot.runId}`);
  const split = timeSplitWords(snapshot);
  if (split) lines.push(`time ${split}`);
  const budget = budgetShare(snapshot);
  if (budget) lines.push(`${budget.percent}% of the budget spent${snapshot.budgetTier ? `, tier ${snapshot.budgetTier}` : ""}`);
  if (snapshot.reservedGiMinutes != null) lines.push(`reserved ${snapshot.reservedGiMinutes.toFixed(1)} Gi·min`);
  if (snapshot.workShapeReason) lines.push(`shape ${snapshot.workShape ?? "stated"}: ${snapshot.workShapeReason}`);
  if (sandbox && !isTerminal(snapshot)) {
    lines.push(`sandbox ${sandbox.commands} commands${sandbox.lastCommand ? `, last ${sandbox.lastCommand}` : ""}`);
  }
  return lines;
}
