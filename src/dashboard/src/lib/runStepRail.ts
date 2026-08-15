import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import type { RunStepRow } from "@/lib/runStepsApi";

// p0388b: the execution rail, built from the RunStep projection rows instead of
// folded from the replayed event log. One row in, one row out — no ordering
// reconstruction, so the rail is complete even when the client's live buffer
// holds nothing at all.

const STEP_ID_PREFIX = "step-";

// p0395: the shape the server splices for phase steps (p0393a) — "p19106a:
// Generate plan". Current servers split it into RunStepView.PhaseId before it
// reaches the client; this is the defensive split for payloads from servers
// that predate that, so old runs still render the real step name.
const PHASE_PREFIX_RE = /^(p\d+[a-z]?): (.+)$/;

export function splitPhasePrefix(label: string): { phaseId: string | null; label: string } {
  const match = PHASE_PREFIX_RE.exec(label);
  return match ? { phaseId: match[1], label: match[2] } : { phaseId: null, label };
}

export function toRailNodes(steps: RunStepRow[]): ExecutionNodeProps[] {
  const totalSeconds = Math.max(1, steps.reduce((acc, s) => acc + (s.durationSeconds ?? 0), 0));
  let elapsed = 0;
  return steps.map((s) => {
    const duration = s.durationSeconds ?? 0;
    const startSeconds = elapsed;
    elapsed += duration;
    const split = splitPhasePrefix(s.displayName ?? s.stepName);
    return {
      id: stepNodeId(s.stepIndex),
      label: split.label,
      phaseId: s.phaseId ?? split.phaseId,
      status: railStatus(s.status),
      depth: 0,
      startSeconds,
      durationSeconds: duration,
      totalSeconds,
      durationLabel: duration > 0 ? formatDuration(duration) : "",
      message: s.resultMessage,
      costBadge: composeCostBadge(s),
      timeBadge: composeTimeBadge(s),
      stepClass: s.stepClass ?? null,
      hasFinding: s.hasFinding ?? false,
      // p0405: the server marks what has not been reached; the rail renders it
      // subordinate. It does not decide which steps those are.
      planned: s.planned === true,
    };
  });
}

// p0404: the step's wall-clock, split the way the server already decided it —
// model (with its throttle share), sandbox, and the scaffolding remainder. The
// sandbox part is read against sandboxCommands: N commands whose summed time
// approaches the step's own ran one after another.
export function composeTimeBadge(step: RunStepRow): string | null {
  const time = step.time;
  if (!time) return null;
  if (time.modelMs === 0 && time.sandboxMs === 0) return null;
  const parts = [`${formatMs(time.modelMs)} model`];
  if (time.throttleMs > 0) parts.push(`${formatMs(time.throttleMs)} throttled`);
  if (time.sandboxMs > 0) {
    const commands = step.sandboxCommands ? ` (${step.sandboxCommands} cmd)` : "";
    parts.push(`${formatMs(time.sandboxMs)} sandbox${commands}`);
  }
  // Null while the step is still running: there is no duration to subtract from
  // yet, so the remainder is unknown rather than zero.
  if (time.scaffoldingMs !== null) parts.push(`${formatMs(time.scaffoldingMs)} scaffolding`);
  return parts.join(" · ");
}

function formatMs(ms: number): string {
  return formatDuration(ms / 1000);
}

// p0398: whether a row belongs in the drawer's DEFAULT view — the run's story.
// Milestones always show (missing class from an old server reads as milestone,
// so nothing is ever silently hidden); a gate shows when the server decided it
// has something to say; internals collapse into the mechanics row. Anything
// currently failing, running, cancelled, or waiting for input shows regardless
// of class — a failure IS readable output, whatever step produced it.
export function isStoryRow(node: {
  status: NodeStatus;
  stepClass?: string | null;
  hasFinding?: boolean;
}): boolean {
  if (node.status === "fail" || node.status === "run" || node.status === "cancel" || node.status === "input") {
    return true;
  }
  if (node.stepClass === "internal") return false;
  if (node.stepClass === "gate") return node.hasFinding === true;
  return true;
}

export function stepNodeId(stepIndex: number): string {
  return `${STEP_ID_PREFIX}${stepIndex}`;
}

/** The rail id's step index, or null when the selection is an overview entry. */
export function stepIndexOf(nodeId: string): number | null {
  if (!nodeId.startsWith(STEP_ID_PREFIX)) return null;
  const parsed = Number.parseInt(nodeId.slice(STEP_ID_PREFIX.length), 10);
  return Number.isNaN(parsed) ? null : parsed;
}

// The projection's own status words; anything else — including the absent status
// of a p0405 planned step — reads as not-yet-run rather than being guessed into a
// terminal state.
function railStatus(status: string | null): NodeStatus {
  if (status === "success") return "ok";
  if (status === "failed") return "fail";
  if (status === "running") return "run";
  if (status === "cancelled") return "cancel";
  return "wait";
}

// p0388b: the per-step rollup now comes from the attributed child rows, so the
// badge is a straight read instead of a sum over whatever events were buffered.
function composeCostBadge(step: RunStepRow): string | null {
  if (!step.llmCalls) return null;
  return `$${(step.costUsd ?? 0).toFixed(4)} · ${step.llmCalls} LLM`;
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const mins = Math.floor(seconds / 60);
  return `${mins}m ${Math.round(seconds - mins * 60)}s`;
}
