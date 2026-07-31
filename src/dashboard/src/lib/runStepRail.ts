import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import type { RunStepRow } from "@/lib/runStepsApi";

// p0388b: the execution rail, built from the RunStep projection rows instead of
// folded from the replayed event log. One row in, one row out — no ordering
// reconstruction, so the rail is complete even when the client's live buffer
// holds nothing at all.

const STEP_ID_PREFIX = "step-";

export function toRailNodes(steps: RunStepRow[]): ExecutionNodeProps[] {
  const totalSeconds = Math.max(1, steps.reduce((acc, s) => acc + (s.durationSeconds ?? 0), 0));
  let elapsed = 0;
  return steps.map((s) => {
    const duration = s.durationSeconds ?? 0;
    const startSeconds = elapsed;
    elapsed += duration;
    return {
      id: stepNodeId(s.stepIndex),
      label: s.displayName ?? s.stepName,
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
