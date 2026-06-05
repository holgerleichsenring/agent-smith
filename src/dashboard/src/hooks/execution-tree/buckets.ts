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

// p0228: one entry in a step's CHRONOLOGICAL action timeline. Unlike
// SandboxRepoSnapshot (one row per repo, last command wins), every command
// the agent runs is kept in order so the operator can see exactly what the
// LLM did — what it read, searched (Grep/find), listed, and whether it ever
// actually wrote a source file. exitCode/durationMs are filled in when the
// paired SandboxResult arrives.
export interface SandboxCommandEntry {
  repo: string;
  /** The tool/verb: ReadFile, Grep, ListFiles, WriteFile, git, /bin/sh, … */
  verb: string;
  /** Producer-curated one-liner: the path read, the pattern grepped, the
   *  shell command run. Null when the producer judged it unsafe to surface. */
  summary: string | null;
  exitCode: number | null;
  durationMs: number | null;
  timestamp: string;
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
  // p0228: full chronological command sequence (not collapsed per repo).
  commands: SandboxCommandEntry[];
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
    commands: [],
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
