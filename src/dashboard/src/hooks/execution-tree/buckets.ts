import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { NodeStatus } from "@/components/execution/TimingGutter";

// p0203: step + sub-agent bucket types shared by useRunExecutionTree
// and its rollup helpers. Pure data — no React, no JSX — so the hook
// can compose buckets, then hand them off to renderers.

export interface SandboxRepoSnapshot {
  repo: string;
  command: string | null;
  commandSummary: string | null;
  exitCode: number | null;
  durationMs: number | null;
}

export interface StepBucket {
  index: number;
  name: string;
  displayName: string | null;
  message: string | null;
  startMs: number;
  endMs: number | null;
  status: NodeStatus;
  events: RunEvent[];
  sandboxRepos: Map<string, SandboxRepoSnapshot>;
}

export interface SubAgentBucket {
  id: string;
  name: string;
  activity: string;
  spawnStepIndex: number;
  startMs: number;
  endMs: number | null;
  status: NodeStatus;
  events: RunEvent[];
  totals: {
    observations: number;
    findings: number;
    files: number;
    tools: number;
  };
}

export function statusFromString(s: string): NodeStatus {
  const v = s.toLowerCase();
  if (v === "success" || v === "succeeded" || v === "ok") return "ok";
  if (v === "failed" || v === "fail" || v === "error") return "fail";
  if (v === "running" || v === "started") return "run";
  return "wait";
}

export function resolveRunStartMs(events: RunEvent[], snapshot: RunSnapshot | null): number {
  const started = events.find((e): e is RunEvent & { type: EventType.RunStarted } =>
    e.type === EventType.RunStarted,
  );
  if (started) return Date.parse(started.startedAt);
  if (snapshot?.startedAt) return Date.parse(snapshot.startedAt);
  return Date.parse(events[0].timestamp);
}

export function resolveRunEndMs(events: RunEvent[], snapshot: RunSnapshot | null): number | null {
  const finished = events.find((e): e is RunEvent & { type: EventType.RunFinished } =>
    e.type === EventType.RunFinished,
  );
  if (finished) return Date.parse(finished.finishedAt);
  if (snapshot?.finishedAt) return Date.parse(snapshot.finishedAt);
  return null;
}

export function ingestStepStarted(
  steps: Map<number, StepBucket>, e: Extract<RunEvent, { type: EventType.StepStarted }>,
): number {
  const bucket: StepBucket = {
    index: e.stepIndex,
    name: e.stepName,
    displayName: e.displayName,
    message: null,
    startMs: Date.parse(e.timestamp),
    endMs: null,
    status: "run",
    events: [],
    sandboxRepos: new Map(),
  };
  steps.set(e.stepIndex, bucket);
  return e.stepIndex;
}

export function ingestStepFinished(
  steps: Map<number, StepBucket>, e: Extract<RunEvent, { type: EventType.StepFinished }>,
): void {
  const bucket = steps.get(e.stepIndex);
  if (!bucket) return;
  bucket.endMs = bucket.startMs + e.durationMs;
  bucket.status = statusFromString(e.status);
  bucket.message = e.reason;
}
