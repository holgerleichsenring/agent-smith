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
    };
  });
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

// The projection's own status words; anything else reads as not-yet-run rather
// than being guessed into a terminal state.
function railStatus(status: string): NodeStatus {
  if (status === "success") return "ok";
  if (status === "failed") return "fail";
  if (status === "running") return "run";
  if (status === "cancelled") return "cancel";
  return "wait";
}

// p0388b: the per-step rollup now comes from the attributed child rows, so the
// badge is a straight read instead of a sum over whatever events were buffered.
function composeCostBadge(step: RunStepRow): string | null {
  if (step.llmCalls === 0) return null;
  return `$${step.costUsd.toFixed(4)} · ${step.llmCalls} LLM`;
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const mins = Math.floor(seconds / 60);
  return `${mins}m ${Math.round(seconds - mins * 60)}s`;
}
