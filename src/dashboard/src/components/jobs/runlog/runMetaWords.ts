import { isRelaunching, relaunchPlace } from "@/components/jobs/runStatus";
import type { T } from "@/lib/i18n/translate";
import type { RunSnapshot } from "@/types/hub-events";
import { budgetShare, computeOf, isTerminal, timeSplitWords } from "./runFactWords";

// 2026-11-17-6d9ea: the run page's one meta line and the details behind it. The meta line says the state, the steps,
// the time, the cost against its cap, the compute and the shape; every other fact the run recorded is a details line.

export type StateTone = "ok" | "run" | "amber" | "person" | "bad" | "quiet";

/** The state word the meta line starts with, and its colour. */
export function stateWords(t: T, snapshot: RunSnapshot): { text: string; tone: StateTone } {
  const status = (snapshot.status ?? "").toLowerCase();
  if (isRelaunching(snapshot)) {
    const place = relaunchPlace(snapshot);
    return { text: place != null ? t("runs.log.state.resumingPlace", { place }) : t("runs.log.state.resuming"), tone: "run" };
  }
  switch (status) {
    case "running": return { text: t("runs.status.running"), tone: "run" };
    case "queued": return { text: snapshot.queuePosition ? t("runs.log.state.queuedPlace", { place: snapshot.queuePosition }) : t("runs.log.state.queued"), tone: "run" };
    // 2026-11-22-7103a: a question waits on a person — the status line's words and colour.
    case "waiting_for_input": return { text: t("runs.log.state.waits"), tone: "person" };
    case "success": return { text: t("runs.log.state.done"), tone: "ok" };
    // 2026-11-22-7103a: a shortfall stopped before its contract — red, as the status line draws it.
    case "shortfall": return { text: t("runs.status.shortfall"), tone: "bad" };
    case "cancelled": return { text: t("runs.status.cancelled"), tone: "quiet" };
    case "failed": case "error": return { text: t("runs.status.failed"), tone: "bad" };
    case "": return { text: t("runs.log.state.reading"), tone: "quiet" };
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
export function elapsedWords(t: T, snapshot: RunSnapshot, nowMs: number = Date.now()): string | null {
  const start = Date.parse(snapshot.startedAt);
  if (Number.isNaN(start)) return null;
  const end = snapshot.finishedAt ? Date.parse(snapshot.finishedAt) : nowMs;
  const span = spanWords((end - start) / 1000);
  if (!span) return null;
  return snapshot.finishedAt || isTerminal(snapshot) ? span : t("runs.log.soFar", { span });
}

export interface MetaPart {
  key: string;
  text: string;
  tone?: StateTone | "near" | "over";
}

/** The meta line: state · steps · elapsed · cost of cap · pods · mem · shape, each only when recorded. */
export function metaParts(
  t: T, snapshot: RunSnapshot, stepCount: number, nowMs: number = Date.now(), status?: { text: string; tone: StateTone },
): MetaPart[] {
  // 2026-11-22-7103a: the run page passes the status line's head, so the state word and How it ended say one thing.
  const state = status ?? stateWords(t, snapshot);
  const parts: MetaPart[] = [{ key: "state", text: state.text, tone: state.tone }];
  if (!isTerminal(snapshot) && snapshot.totalSteps > 0) parts.push({ key: "steps", text: t("runs.step.of", { index: snapshot.stepIndex, total: snapshot.totalSteps }) });
  else if (stepCount > 0) parts.push({ key: "steps", text: t("runs.log.steps", { count: stepCount }) });
  const elapsed = elapsedWords(t, snapshot, nowMs);
  if (elapsed) parts.push({ key: "elapsed", text: elapsed });
  const cap = snapshot.budgetCapUsd;
  if (cap != null && cap > 0) {
    const budget = budgetShare(snapshot);
    parts.push({ key: "cost", text: t("runs.log.costOfCap", { cost: snapshot.costUsd.toFixed(2), cap: cap.toFixed(2) }), tone: budget && budget.tone !== "ok" ? budget.tone : undefined });
  } else if (snapshot.costUsd > 0) {
    parts.push({ key: "cost", text: `$${snapshot.costUsd.toFixed(2)}` });
  }
  const pods = snapshot.liveCompute?.pods ?? [];
  if (pods.length > 0) {
    parts.push({ key: "pods", text: t("runs.log.pods", { count: pods.length }) });
    if (snapshot.liveCompute?.totalMem) parts.push({ key: "mem", text: snapshot.liveCompute.totalMem });
  } else if (computeOf(t, snapshot)) {
    parts.push({ key: "pods", text: t("runs.log.computeCalculating") });
  }
  if (snapshot.workShape) parts.push({ key: "shape", text: t("runs.log.shape", { shape: snapshot.workShape }) });
  return parts;
}

export interface SandboxBeat {
  commands: number;
  lastCommand?: string | null;
}

/** Everything else the run recorded, one line each, for the details under the meta line. */
export function detailLines(t: T, locale: string, snapshot: RunSnapshot, repoNames: string[], sandbox: SandboxBeat | null): string[] {
  const lines: string[] = [];
  const at = (iso: string) => new Date(iso).toLocaleString(locale, {
    day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false,
  });
  if (!Number.isNaN(Date.parse(snapshot.startedAt))) {
    lines.push(snapshot.finishedAt
      ? t("runs.log.detail.startedFinished", { start: at(snapshot.startedAt), end: at(snapshot.finishedAt) })
      : t("runs.log.detail.started", { start: at(snapshot.startedAt) }));
  }
  if (snapshot.agentName || snapshot.llmCalls > 0) {
    const calls = t("runs.log.detail.calls", { count: snapshot.llmCalls });
    lines.push(snapshot.agentName ? t("runs.log.detail.model", { agent: snapshot.agentName, calls }) : calls);
  }
  if (repoNames.length > 0) lines.push(t("runs.log.detail.repos", { count: repoNames.length, names: repoNames.join(", ") }));
  if (snapshot.pipeline) lines.push(t("runs.log.detail.pipeline", { pipeline: snapshot.pipeline }));
  lines.push(t("runs.log.detail.run", { id: snapshot.runId }));
  const split = timeSplitWords(t, snapshot);
  if (split) lines.push(t("runs.log.detail.time", { split }));
  const budget = budgetShare(snapshot);
  if (budget) {
    lines.push(snapshot.budgetTier
      ? t("runs.log.detail.budgetTier", { percent: budget.percent, tier: snapshot.budgetTier })
      : t("runs.log.detail.budget", { percent: budget.percent }));
  }
  if (snapshot.reservedGiMinutes != null) lines.push(t("runs.log.detail.reserved", { amount: snapshot.reservedGiMinutes.toFixed(1) }));
  if (snapshot.workShapeReason) lines.push(t("runs.log.detail.shapeReason", { shape: snapshot.workShape ?? t("runs.log.detail.shapeStated"), reason: snapshot.workShapeReason }));
  if (sandbox && !isTerminal(snapshot)) {
    lines.push(sandbox.lastCommand
      ? t("runs.log.detail.sandboxLast", { count: sandbox.commands, command: sandbox.lastCommand })
      : t("runs.log.detail.sandbox", { count: sandbox.commands }));
  }
  return lines;
}
