import { instruction, stuckTitle } from "@/components/tickets/detail/yourMove";
import { isRelaunching, relaunchPlace } from "@/components/jobs/runStatus";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { hasPullRequest } from "@/lib/prStatus";
import { extractUrls, stripUrls } from "@/lib/prUrls";
import type { RunStepRow } from "@/lib/runStepsApi";
import { splitSpecPrefix } from "@/lib/runStepRail";
import {
  EventType,
  type PullRequestStatus,
  type RunEvent,
  type RunSnapshot,
  type VerifyPassRecordedEvent,
} from "@/types/hub-events";
import type { TicketAttempt } from "@/types/tickets";
import { isTerminal } from "./runFactWords";
import { commandKey } from "./runSections";

// 2026-11-17-6d9ea: the Now block's lines. While the run works: the step it is in with what it says it is doing, then
// the newest decisions, tool calls, files written and findings off the run's hub events, and the sandbox beat. Once it
// has ended: how (status, pull requests, criteria, cost), what the review and the last verify pass said, and why it
// stopped with what to do about it. SandboxCommand is not read: the live hub never sends it, only a reload's replay.

export type NowTone = "now" | "text" | "dim" | "ok" | "run" | "bad";

export interface NowLine {
  key: string;
  /** "13:50:41"; null when the fact carries no time. */
  at: string | null;
  kind: string;
  text: string;
  tone: NowTone;
}

export type NowMode = "live" | "waiting" | "queued" | "ended";

export function nowMode(snapshot: RunSnapshot): NowMode {
  if (isTerminal(snapshot)) return "ended";
  const status = (snapshot.status ?? "").toLowerCase();
  if (status === "waiting_for_input" && !isRelaunching(snapshot)) return "waiting";
  if (status === "queued" || isRelaunching(snapshot)) return "queued";
  return "live";
}

const LABELS: Record<NowMode, string> = {
  live: "Now — follows the agent",
  waiting: "Waiting for your answer",
  queued: "Waiting for capacity",
  ended: "How it ended",
};

export function nowLabel(snapshot: RunSnapshot): string {
  return LABELS[nowMode(snapshot)];
}

export function clockOf(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const at = new Date(iso);
  return Number.isNaN(at.getTime()) ? null : at.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false });
}

function stepLabel(row: RunStepRow): string {
  return splitSpecPrefix(row.displayName ?? row.stepName).label;
}

/** One of the agent's acts the Now block shows, or null for an event it does not. */
function actLine(e: RunEvent, i: number): NowLine | null {
  const at = clockOf(e.timestamp);
  const key = `${e.type}-${i}`;
  switch (e.type) {
    case EventType.DecisionLogged: return { key, at, kind: "decision", text: e.chose, tone: "text" };
    case EventType.SubAgentFileWritten: return { key, at, kind: "wrote", text: `${e.path} (${e.bytes} bytes)`, tone: "dim" };
    case EventType.ToolCall: return { key, at, kind: "tool", text: e.summary ?? e.tool, tone: "dim" };
    case EventType.SubAgentToolCall: return { key, at, kind: "tool", text: e.argsSummary ?? e.toolName, tone: "dim" };
    case EventType.SubAgentFinding: return { key, at, kind: "finding", text: `${e.severity} · ${e.title}`, tone: "run" };
    case EventType.GateChecked: return { key, at, kind: "gate", text: `${e.gate} ${e.passed ? "passed" : "failed"}${e.reason ? `: ${e.reason}` : ""}`, tone: e.passed ? "dim" : "bad" };
    default: return null;
  }
}

/** The step the run is in: the projection's running row, else the newest StepStarted without its finish. */
function currentStep(events: RunEvent[], steps: RunStepRow[]): { label: string; index: number } | null {
  const running = [...steps].reverse().find((s) => s.status === "running");
  if (running) return { label: stepLabel(running), index: running.stepIndex };
  const finished = new Set<number>();
  for (let i = events.length - 1; i >= 0; i--) {
    const e = events[i];
    if (e.type === EventType.StepFinished) finished.add(e.stepIndex);
    else if (e.type === EventType.StepStarted && !finished.has(e.stepIndex)) {
      return { label: splitSpecPrefix(e.displayName ?? e.stepName).label, index: e.stepIndex };
    }
  }
  return null;
}

/** While the run works: the step with its newest narration, the sandbox beat, then the newest `max` acts. */
export function liveLines(
  snapshot: RunSnapshot, events: RunEvent[], steps: RunStepRow[],
  beat: { lastCommand?: string | null } | null, max = 3,
): NowLine[] {
  const step = currentStep(events, steps);
  const lines: NowLine[] = [];
  let narration: RunEvent | null = null;
  for (let i = events.length - 1; i >= 0 && !narration; i--) {
    const e = events[i];
    if (e.type === EventType.StepStarted) break;
    if (e.type === EventType.ToolCall && e.intent) narration = e;
  }
  const said = narration && narration.type === EventType.ToolCall ? narration.intent : null;
  lines.push({
    key: "step",
    at: clockOf(narration?.timestamp ?? null),
    kind: step?.label ?? snapshot.stepName ?? "working",
    text: said ?? (step || snapshot.stepName ? "working on it" : "starting"),
    tone: "now",
  });
  if (beat?.lastCommand) lines.push({ key: "beat", at: null, kind: "ran", text: beat.lastCommand, tone: "dim" });
  const acts: NowLine[] = [];
  for (let i = events.length - 1; i >= 0 && acts.length < max; i--) {
    const line = actLine(events[i], i);
    if (line) acts.push(line);
  }
  return [...lines, ...acts];
}

/** A waiting or queued run's lines: what it waits for and that its compute is held. */
export function waitLines(snapshot: RunSnapshot): NowLine[] {
  const status = (snapshot.status ?? "").toLowerCase();
  if (isRelaunching(snapshot)) {
    const place = relaunchPlace(snapshot);
    return [{ key: "resume", at: null, kind: "resuming", text: `Answered${place != null ? `, place ${place} in line` : ""}. Nothing for you to do.`, tone: "run" }];
  }
  if (status === "queued") {
    return [{ key: "queue", at: null, kind: "queued", text: snapshot.queuePosition ? `Place ${snapshot.queuePosition} in line. Nothing for you to do.` : "Waiting for a slot. Nothing for you to do.", tone: "run" }];
  }
  return [{ key: "held", at: clockOf(snapshot.pendingQuestion?.askedAt ?? null), kind: "paused", text: "The agent needs an answer before it continues. Reserved compute is held; no tokens are spent while it waits.", tone: "run" }];
}

export interface PullRequestLink {
  repo: string;
  url: string;
  isDraft: boolean;
}

/** Every pull request the run has: the per-repository list, else the single prUrl, plus a URL inside the summary. */
export function pullRequestLinks(snapshot: RunSnapshot): PullRequestLink[] {
  const delivered = snapshot.status === "success" || snapshot.status === "shortfall";
  const listed = (snapshot.pullRequests ?? [])
    .filter((p) => p.url && hasPullRequest(p.status as PullRequestStatus))
    .map((p) => ({ repo: p.repo, url: p.url, isDraft: p.isDraft }));
  const base = listed.length > 0 ? listed : snapshot.prUrl ? [{ repo: snapshot.repos?.[0] ?? "", url: snapshot.prUrl, isDraft: !delivered }] : [];
  const said = extractUrls(snapshot.summary ? formatRunSummary(snapshot.summary) : null)
    .filter((url) => /\/pull(request)?s?\/\d+|merge_requests\/\d+/i.test(url) && !base.some((p) => p.url === url))
    .map((url) => ({ repo: "", url, isDraft: !delivered }));
  return [...base, ...said];
}

const REVIEW = new Set(["ReviewSpecDiff", "RunReviewPhase"]);
const VERIFY = new Set(["VerifySpec", "RunVerifyPhase"]);

function firstLine(text: string | null | undefined): string | null {
  const line = (text ?? "").split(/\r?\n/).find((l) => l.trim() !== "");
  return line ? line.trim() : null;
}

export interface EndedView {
  lines: NowLine[];
  /** The last verify pass as data, drawn per repository under the lines. */
  verify: VerifyPassRecordedEvent | null;
  /** The setting that lifts a stop, when the end named one. */
  settingsPath: string | null;
}

/** How a finished run ended: outcome, review, verify, and for a stop why and what to do. */
export function endedView(snapshot: RunSnapshot, steps: RunStepRow[], events: RunEvent[], attempt: TicketAttempt | null): EndedView {
  const status = (snapshot.status ?? "").toLowerCase();
  const at = clockOf(snapshot.finishedAt);
  const prs = pullRequestLinks(snapshot);
  const criteria = snapshot.acceptance?.criteria ?? [];
  const stopped = status !== "success" && status !== "shortfall" && status !== "cancelled";
  const head = status === "success" ? "Done" : status === "shortfall" ? "Done, with a shortfall" : status === "cancelled" ? "Cancelled" : stuckTitle(attempt);
  const parts = [head];
  if (status !== "cancelled" || prs.length > 0) {
    parts.push(prs.length === 0 ? "no pull request opened" : prs.length === 1 ? "pull request opened" : `${prs.length} pull requests opened`);
  }
  if (criteria.length > 0) parts.push(`${criteria.filter((c) => c.status === "met").length} of ${criteria.length} criteria proven`);
  if (snapshot.costUsd > 0) parts.push(`$${snapshot.costUsd.toFixed(2)}`);
  const tone: NowTone = status === "success" ? "ok" : status === "shortfall" ? "run" : status === "cancelled" ? "dim" : "bad";
  const lines: NowLine[] = [{ key: "outcome", at, kind: "outcome", text: parts.join(" · "), tone }];

  const summary = snapshot.summary ? stripUrls(formatRunSummary(snapshot.summary)).trim() : "";
  if (summary) lines.push({ key: "said", at: null, kind: stopped || status === "cancelled" ? "why" : "said", text: summary, tone: stopped ? "bad" : "text" });
  if (stopped) {
    const failedStep = [...steps].reverse().find((s) => s.status === "failed" || s.status === "error");
    if (failedStep) {
      const said = firstLine(failedStep.resultMessage);
      lines.push({ key: "stopped-at", at: null, kind: "stopped at", text: said ? `${stepLabel(failedStep)}: ${said}` : stepLabel(failedStep), tone: "bad" });
    }
    const todo = attempt?.endDetail ? instruction(attempt.endDetail) : null;
    if (todo) lines.push({ key: "todo", at: null, kind: "to do", text: todo, tone: "text" });
  }

  const reached = steps.filter((s) => !s.planned && s.status);
  const review = [...reached].reverse().find((s) => REVIEW.has(commandKey(s)));
  if (review) {
    lines.push({ key: "review", at: null, kind: "review", text: firstLine(review.resultMessage) ?? "diff reviewed", tone: review.hasFinding ? "run" : "text" });
  }
  let verify: VerifyPassRecordedEvent | null = null;
  for (let i = events.length - 1; i >= 0 && !verify; i--) {
    const e = events[i];
    if (e.type === EventType.VerifyPassRecorded) verify = e;
  }
  if (!verify) {
    const step = [...reached].reverse().find((s) => VERIFY.has(commandKey(s)));
    if (step) lines.push({ key: "verify", at: null, kind: "verify", text: firstLine(step.resultMessage) ?? step.status ?? "", tone: step.status === "failed" ? "bad" : "text" });
  }
  return { lines, verify, settingsPath: stopped ? attempt?.endDetail?.settingsPath ?? null : null };
}
