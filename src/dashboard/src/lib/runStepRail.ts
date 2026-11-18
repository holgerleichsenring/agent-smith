import type { NodeStatus } from "@/components/execution/TimingGutter";
import type { RunStepRow } from "@/lib/runStepsApi";

// p0388b: the step rows the server projects, read for the run page. 2026-11-17-6d9ed: the graph view's rail is gone;
// what stays is the spec-prefix split, the story/mechanics rule and the step's time split, which the run log reads.

// p0395: the shape the server splices for spec steps (p0393a) — "p19106a:
// Generate plan". Current servers split it into RunStepView.SpecId before it
// reaches the client; this is the defensive split for payloads from servers
// that predate that, so old runs still render the real step name.
// p0507: both id shapes — the counter namespace and a date-minted id. This mirrors
// RunStepsReader.SpecQualifiedRegex; fixing only the backend leaves the raw prefix here.
const SPEC_PREFIX_RE = /^((?:p\d+[a-z]?|\d{4}-\d{2}-\d{2}-[0-9a-f]{4}[a-z]?)): (.+)$/;

export function splitSpecPrefix(label: string): { specId: string | null; label: string } {
  const match = SPEC_PREFIX_RE.exec(label);
  return match ? { specId: match[1], label: match[2] } : { specId: null, label };
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

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const mins = Math.floor(seconds / 60);
  return `${mins}m ${Math.round(seconds - mins * 60)}s`;
}
