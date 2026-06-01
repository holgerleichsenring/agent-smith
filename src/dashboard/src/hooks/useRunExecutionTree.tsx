"use client";

import { useMemo } from "react";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import type { DrawerEvent, EventKind } from "@/components/execution/EventDrawer";
import { EventDrawer } from "@/components/execution/EventDrawer";
import { FetchTicketBody } from "@/components/execution/bodies/FetchTicketBody";
import { StepSandboxes } from "@/components/execution/bodies/StepSandboxes";

// p0183: turn the raw RunEvent stream into the ExecutionNode tree the
// dashboard renders. One row per step (StepStarted/StepFinished pair),
// sub-agents (SubAgentSpawned/Completed) nested as children under whichever
// step was active when the spawn happened. Each step's body holds an
// EventDrawer scoped to that step's typed events.

export interface RunExecutionTree {
  nodes: ExecutionNodeProps[];
  totalSeconds: number;
}

export function useRunExecutionTree(
  events: RunEvent[],
  snapshot: RunSnapshot | null,
  runId: string | null = null,
): RunExecutionTree {
  return useMemo(() => buildTree(events, snapshot, runId), [events, snapshot, runId]);
}

interface StepBucket {
  index: number;
  name: string;
  startMs: number;
  endMs: number | null;
  status: NodeStatus;
  events: RunEvent[];
  /** p0189: repos that issued at least one SandboxCommand during this step.
   *  Surfaces per-repo SandboxBox(es) inside the step body so the operator
   *  sees the live stdout/stderr stream (and command + exit code) without
   *  hunting through the Architecture topology graph. */
  sandboxRepos: Map<string, SandboxRepoSnapshot>;
}

export interface SandboxRepoSnapshot {
  repo: string;
  command: string | null;
  commandSummary: string | null;
  exitCode: number | null;
  durationMs: number | null;
}

interface SubAgentBucket {
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

function buildTree(
  events: RunEvent[],
  snapshot: RunSnapshot | null,
  runId: string | null,
): RunExecutionTree {
  if (events.length === 0) {
    return { nodes: [], totalSeconds: 1 };
  }

  const runStartMs = resolveRunStartMs(events, snapshot);
  const runEndMs = resolveRunEndMs(events, snapshot);
  const nowFallbackMs = Math.max(runStartMs, runEndMs ?? runStartMs);

  const steps = new Map<number, StepBucket>();
  const subAgents = new Map<string, SubAgentBucket>();
  let activeStepIndex = -1;

  for (const e of events) {
    const tMs = parseTs(e.timestamp);
    switch (e.type) {
      case EventType.StepStarted: {
        const bucket: StepBucket = {
          index: e.stepIndex,
          name: e.stepName,
          startMs: tMs,
          endMs: null,
          status: "run",
          events: [],
          sandboxRepos: new Map(),
        };
        steps.set(e.stepIndex, bucket);
        activeStepIndex = e.stepIndex;
        break;
      }
      case EventType.StepFinished: {
        const bucket = steps.get(e.stepIndex);
        if (bucket) {
          bucket.endMs = bucket.startMs + e.durationMs;
          bucket.status = statusFromString(e.status);
        }
        break;
      }
      case EventType.SubAgentSpawned: {
        const sa: SubAgentBucket = {
          id: e.subAgentId,
          name: e.name,
          activity: e.activity,
          spawnStepIndex: activeStepIndex,
          startMs: tMs,
          endMs: null,
          status: "run",
          events: [],
          totals: { observations: 0, findings: 0, files: 0, tools: 0 },
        };
        subAgents.set(e.subAgentId, sa);
        break;
      }
      case EventType.SubAgentObservation:
      case EventType.SubAgentFinding:
      case EventType.SubAgentFileWritten:
      case EventType.SubAgentToolCall: {
        const sa = subAgents.get(e.subAgentId);
        if (sa) {
          sa.events.push(e);
          if (e.type === EventType.SubAgentObservation) sa.totals.observations++;
          if (e.type === EventType.SubAgentFinding) sa.totals.findings++;
          if (e.type === EventType.SubAgentFileWritten) sa.totals.files++;
          if (e.type === EventType.SubAgentToolCall) sa.totals.tools++;
        }
        break;
      }
      case EventType.SubAgentCompleted: {
        const sa = subAgents.get(e.subAgentId);
        if (sa) {
          sa.endMs = tMs;
          sa.status = statusFromString(e.status);
        }
        break;
      }
      case EventType.L1StepDetail:
      case EventType.DecisionLogged:
      case EventType.GateChecked:
      case EventType.ToolCall:
      case EventType.ToolResult:
      case EventType.LlmCallStarted:
      case EventType.LlmCallFinished:
      case EventType.TicketFetched: {
        const bucket = steps.get(activeStepIndex);
        if (bucket) bucket.events.push(e);
        break;
      }
      case EventType.SandboxCommand: {
        const bucket = steps.get(activeStepIndex);
        if (bucket) {
          const prev = bucket.sandboxRepos.get(e.repo);
          bucket.sandboxRepos.set(e.repo, {
            repo: e.repo,
            command: e.command,
            commandSummary: e.summary,
            exitCode: prev?.exitCode ?? null,
            durationMs: prev?.durationMs ?? null,
          });
        }
        break;
      }
      case EventType.SandboxResult: {
        const bucket = steps.get(activeStepIndex);
        if (bucket) {
          const prev = bucket.sandboxRepos.get(e.repo);
          bucket.sandboxRepos.set(e.repo, {
            repo: e.repo,
            command: prev?.command ?? e.command,
            commandSummary: prev?.commandSummary ?? null,
            exitCode: e.exitCode,
            durationMs: e.durationMs,
          });
        }
        break;
      }
      default:
        break;
    }
  }

  const totalMs = Math.max(1, (runEndMs ?? nowFallbackMs) - runStartMs);
  const totalSeconds = totalMs / 1000;

  const subAgentsByStep = new Map<number, SubAgentBucket[]>();
  for (const sa of subAgents.values()) {
    const list = subAgentsByStep.get(sa.spawnStepIndex) ?? [];
    list.push(sa);
    subAgentsByStep.set(sa.spawnStepIndex, list);
  }

  const orderedSteps = [...steps.values()].sort((a, b) => a.index - b.index);
  const nowMs = nowFallbackMs;
  const nodes: ExecutionNodeProps[] = orderedSteps.map((s) => stepBucketToNode(
    s,
    runStartMs,
    nowMs,
    totalSeconds,
    subAgentsByStep.get(s.index) ?? [],
    runId,
  ));

  return { nodes, totalSeconds };
}

function stepBucketToNode(
  s: StepBucket,
  runStartMs: number,
  nowMs: number,
  totalSeconds: number,
  subAgents: SubAgentBucket[],
  runId: string | null,
): ExecutionNodeProps {
  const startSec = (s.startMs - runStartMs) / 1000;
  const endSec = ((s.endMs ?? nowMs) - runStartMs) / 1000;
  const durationSec = Math.max(0, endSec - startSec);
  const tail = pickStepTail(s);
  const drawerEvents = mapToDrawerEvents(s.events);
  const children = subAgents.map((sa) =>
    subAgentBucketToNode(sa, runStartMs, nowMs, totalSeconds),
  );
  // p0184: Fetch-ticket step body shows the ticket details from the
  // typed TicketFetchedEvent instead of (or alongside) the generic
  // event drawer. Detected by the presence of any TicketFetchedEvent
  // in this step's bucket — robust against step-label drift.
  const hasTicketEvent = s.events.some((e) => e.type === EventType.TicketFetched);
  // p0189: steps that issued any SandboxCommand surface per-repo
  // SandboxBox(es) inline so the operator sees stdout/stderr without
  // navigating elsewhere. Live for the active step; for finished steps
  // the buffer is empty but command + exit code still render.
  const sandboxRepos = [...s.sandboxRepos.values()];
  const sandboxBody = sandboxRepos.length > 0 && runId
    ? <StepSandboxes runId={runId} sandboxes={sandboxRepos} />
    : null;
  const primaryBody = hasTicketEvent
    ? <FetchTicketBody events={s.events} />
    : drawerEvents.length > 0 ? <EventDrawer events={drawerEvents} /> : null;
  const body = sandboxBody && primaryBody
    ? <>{sandboxBody}{primaryBody}</>
    : sandboxBody ?? primaryBody;
  return {
    id: `step-${s.index}`,
    label: s.name,
    status: s.status,
    depth: 0,
    startSeconds: Math.max(0, startSec),
    durationSeconds: durationSec,
    totalSeconds,
    durationLabel: formatDuration(durationSec),
    tail,
    body,
    children,
  };
}

function subAgentBucketToNode(
  sa: SubAgentBucket,
  runStartMs: number,
  nowMs: number,
  totalSeconds: number,
): ExecutionNodeProps {
  const startSec = (sa.startMs - runStartMs) / 1000;
  const endSec = ((sa.endMs ?? nowMs) - runStartMs) / 1000;
  const durationSec = Math.max(0, endSec - startSec);
  const drawerEvents = mapToDrawerEvents(sa.events);
  return {
    id: `sub-${sa.id}`,
    label: `sub-agent: ${sa.name}`,
    labelMono: true,
    status: sa.status,
    depth: 1,
    startSeconds: Math.max(0, startSec),
    durationSeconds: durationSec,
    totalSeconds,
    durationLabel: formatDuration(durationSec),
    tail: pickTail(sa.events) ?? { text: sa.activity, timestamp: formatHms(sa.startMs) },
    body: <EventDrawer events={drawerEvents} />,
  };
}

function pickTail(es: RunEvent[]): { text: string; timestamp: string } | undefined {
  if (es.length === 0) return undefined;
  const last = [...es].sort((a, b) => b.timestamp.localeCompare(a.timestamp))[0];
  return {
    text: describeEvent(last),
    timestamp: shortTime(last.timestamp),
  };
}

// p0186: every step gets a tail. Prefer the latest typed event description;
// fall back to a status-derived line so steps that only emit
// StepStarted/StepFinished (no L1StepDetail, no decisions, no LLM calls —
// Publishing pipeline name, Checking out source, etc.) still surface a one-
// liner instead of looking empty.
function pickStepTail(s: StepBucket): { text: string; timestamp: string } | undefined {
  const fromEvents = pickTail(s.events);
  if (fromEvents !== undefined) return fromEvents;
  const ts = formatHms(s.endMs ?? s.startMs);
  switch (s.status) {
    case "ok":
      return { text: "done", timestamp: ts };
    case "fail":
      return { text: "failed", timestamp: ts };
    case "run":
      return { text: "running…", timestamp: ts };
    case "wait":
      return undefined;
  }
}

function describeEvent(e: RunEvent): string {
  switch (e.type) {
    case EventType.L1StepDetail:
      return e.detail;
    case EventType.DecisionLogged:
      return `decision · ${e.chose}`;
    case EventType.GateChecked:
      return `gate · ${e.gate} · ${e.passed ? "pass" : "fail"}`;
    case EventType.ToolCall:
      return e.summary ?? `tool · ${e.tool}`;
    case EventType.ToolResult:
      return `${e.tool} · ${e.ok ? "ok" : "fail"}${e.errorMessage ? " · " + e.errorMessage : ""}`;
    case EventType.LlmCallStarted:
      return `LLM start · ${e.model} (${e.role})`;
    case EventType.LlmCallFinished:
      return `LLM · ${e.tokensIn} in / ${e.tokensOut} out · ${(e.durationMs / 1000).toFixed(1)}s`;
    case EventType.SubAgentObservation:
      return e.text;
    case EventType.SubAgentFinding:
      return `${capitalise(e.severity)} · ${e.title}`;
    case EventType.SubAgentFileWritten:
      return `wrote ${e.path}`;
    case EventType.SubAgentToolCall:
      return e.argsSummary ?? `tool · ${e.toolName}`;
    case EventType.TicketFetched:
      return `#${e.ticketId} — ${e.title}`;
    default:
      return EventType[e.type] ?? "event";
  }
}

function mapToDrawerEvents(es: RunEvent[]): DrawerEvent[] {
  const out: DrawerEvent[] = [];
  for (const e of es) {
    const kind = kindOf(e);
    if (!kind) continue;
    out.push({
      id: `${e.type}-${e.timestamp}-${out.length}`,
      timestamp: shortTime(e.timestamp),
      kind,
      severity: severityOf(e),
      body: <span>{describeEvent(e)}</span>,
      searchText: describeEvent(e),
    });
  }
  return out;
}

function kindOf(e: RunEvent): EventKind | null {
  switch (e.type) {
    case EventType.L1StepDetail:
    case EventType.SubAgentObservation:
      return "obs";
    case EventType.SubAgentFinding:
      return "find";
    case EventType.ToolCall:
    case EventType.ToolResult:
    case EventType.SubAgentToolCall:
      return "tool";
    case EventType.LlmCallStarted:
    case EventType.LlmCallFinished:
      return "llm";
    case EventType.SubAgentFileWritten:
      return "file";
    case EventType.DecisionLogged:
    case EventType.GateChecked:
      return "dec";
    default:
      return null;
  }
}

function severityOf(e: RunEvent): "high" | "med" | "info" | undefined {
  if (e.type === EventType.SubAgentFinding) {
    const s = e.severity.toLowerCase();
    if (s === "high" || s === "critical") return "high";
    if (s === "med" || s === "medium" || s === "moderate") return "med";
    return "info";
  }
  return undefined;
}

function statusFromString(s: string): NodeStatus {
  const v = s.toLowerCase();
  if (v === "success" || v === "succeeded" || v === "ok") return "ok";
  if (v === "failed" || v === "fail" || v === "error") return "fail";
  if (v === "running" || v === "started") return "run";
  return "wait";
}

function resolveRunStartMs(events: RunEvent[], snapshot: RunSnapshot | null): number {
  const started = events.find((e): e is RunEvent & { type: EventType.RunStarted } =>
    e.type === EventType.RunStarted,
  );
  if (started) return parseTs(started.startedAt);
  if (snapshot?.startedAt) return parseTs(snapshot.startedAt);
  return parseTs(events[0].timestamp);
}

function resolveRunEndMs(events: RunEvent[], snapshot: RunSnapshot | null): number | null {
  const finished = events.find((e): e is RunEvent & { type: EventType.RunFinished } =>
    e.type === EventType.RunFinished,
  );
  if (finished) return parseTs(finished.finishedAt);
  if (snapshot?.finishedAt) return parseTs(snapshot.finishedAt);
  return null;
}

function parseTs(iso: string): number {
  return new Date(iso).getTime();
}

function shortTime(iso: string): string {
  const d = new Date(iso);
  return d.toISOString().slice(11, 19);
}

function formatHms(ms: number): string {
  return new Date(ms).toISOString().slice(11, 19);
}

function formatDuration(seconds: number): string {
  if (!isFinite(seconds) || seconds <= 0) return "—";
  if (seconds < 1) return `${Math.round(seconds * 1000)}ms`;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const m = Math.floor(seconds / 60);
  const rem = Math.round(seconds - m * 60);
  return rem === 0 ? `${m}m` : `${m}m${rem}s`;
}

function capitalise(s: string): string {
  return s.length === 0 ? s : s[0].toUpperCase() + s.slice(1);
}
