"use client";

import { useMemo } from "react";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import { EventDrawer } from "@/components/execution/EventDrawer";
import { FetchTicketBody } from "@/components/execution/bodies/FetchTicketBody";
import { CatalogLoadBody } from "@/components/execution/bodies/CatalogLoadBody";
import { StepSandboxes } from "@/components/execution/bodies/StepSandboxes";
import { CommandTimeline } from "@/components/execution/bodies/CommandTimeline";
import { LlmCallsBody } from "@/components/execution/bodies/LlmCallsBody";
import { PrOutcomeList } from "@/components/execution/bodies/PrOutcomeList";
import { pairLlmCalls, type PairedLlmCall } from "./execution-tree/llmPairing";
import {
  buildRepoRollup,
  extractBaseCommand,
  isMultiRepoCommand,
  type RepoRollup,
} from "./execution-tree/perRepoAggregator";
import {
  formatDuration,
  formatHms,
  mapToDrawerEvents,
  describeEvent,
  shortTime,
} from "./execution-tree/eventDescriptors";
import {
  ingestStepFinished,
  ingestStepStarted,
  resolveRunEndMs,
  resolveRunStartMs,
  statusFromString,
  type StepBucket,
  type SubAgentBucket,
} from "./execution-tree/buckets";

// p0183 / p0203: turn the raw RunEvent stream into the ExecutionNode tree
// the dashboard renders. One row per step (StepStarted/StepFinished pair),
// sub-agents (SubAgentSpawned/Completed) nested as children. p0203 added:
// - displayName / message surfaced on the row (operator-facing label +
//   handler outcome under the row instead of bare "done"),
// - paired LLM rows + per-step cost rollup (handled in this hook, not the
//   renderer — keeps EventDrawer schema-stable),
// - per-repo aggregation: whitelisted commands collapse N adjacent
//   per-repo step buckets into a synthetic parent with N/M summary +
//   failed-repo names. Children render collapsed by default.

export type { SandboxRepoSnapshot } from "./execution-tree/buckets";

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

function buildTree(
  events: RunEvent[],
  snapshot: RunSnapshot | null,
  runId: string | null,
): RunExecutionTree {
  if (events.length === 0) return { nodes: [], totalSeconds: 1 };

  const runStartMs = resolveRunStartMs(events, snapshot);
  const runEndMs = resolveRunEndMs(events, snapshot);
  const nowFallbackMs = Math.max(runStartMs, runEndMs ?? runStartMs);
  const steps = new Map<number, StepBucket>();
  const subAgents = new Map<string, SubAgentBucket>();
  ingestEvents(events, steps, subAgents);

  const totalMs = Math.max(1, (runEndMs ?? nowFallbackMs) - runStartMs);
  const totalSeconds = totalMs / 1000;
  const subAgentsByStep = groupSubAgentsByStep(subAgents);
  const orderedSteps = [...steps.values()].sort((a, b) => a.index - b.index);

  // p0227: a run is "ended" (terminal) once its status is anything but running.
  // An LLM call that never got its Finished event on an ended run wasn't truly
  // in flight — it was cut off when the run stopped, so the row reads "ended".
  const runEnded = !!snapshot?.status && snapshot.status !== "running";
  const stepNodes = orderedSteps.map((s) => stepBucketToNode(
    s, runStartMs, nowFallbackMs, totalSeconds, subAgentsByStep.get(s.index) ?? [], runId, runEnded,
  ));
  const collapsed = collapseMultiRepoSiblings(stepNodes, orderedSteps);
  return { nodes: collapsed, totalSeconds };
}

function ingestEvents(
  events: RunEvent[], steps: Map<number, StepBucket>, subAgents: Map<string, SubAgentBucket>,
): void {
  let activeStepIndex = -1;
  for (const e of events) {
    switch (e.type) {
      case EventType.StepStarted: activeStepIndex = ingestStepStarted(steps, e); break;
      case EventType.StepFinished: ingestStepFinished(steps, e); break;
      case EventType.SubAgentSpawned: ingestSubAgentSpawn(subAgents, e, activeStepIndex); break;
      case EventType.SubAgentObservation:
      case EventType.SubAgentFinding:
      case EventType.SubAgentFileWritten:
      case EventType.SubAgentToolCall: ingestSubAgentEvent(subAgents, e); break;
      case EventType.SubAgentCompleted: ingestSubAgentCompleted(subAgents, e); break;
      case EventType.SandboxCommand: ingestSandboxCommand(steps, activeStepIndex, e); break;
      case EventType.SandboxResult: ingestSandboxResult(steps, activeStepIndex, e); break;
      default: attachToActiveStep(steps, activeStepIndex, e); break;
    }
  }
}

function attachToActiveStep(
  steps: Map<number, StepBucket>, activeStepIndex: number, e: RunEvent,
): void {
  if (e.type === EventType.RunStarted || e.type === EventType.RunFinished) return;
  if (e.type === EventType.StepStarted || e.type === EventType.StepFinished) return;
  const bucket = steps.get(activeStepIndex);
  if (bucket) bucket.events.push(e);
}

function ingestSubAgentSpawn(
  subAgents: Map<string, SubAgentBucket>,
  e: Extract<RunEvent, { type: EventType.SubAgentSpawned }>,
  spawnStepIndex: number,
): void {
  subAgents.set(e.subAgentId, {
    id: e.subAgentId, name: e.name, activity: e.activity,
    spawnStepIndex, startMs: Date.parse(e.timestamp), endMs: null, status: "run",
    events: [], totals: { observations: 0, findings: 0, files: 0, tools: 0 },
  });
}

function ingestSubAgentEvent(
  subAgents: Map<string, SubAgentBucket>,
  e: Extract<RunEvent, {
    type: EventType.SubAgentObservation | EventType.SubAgentFinding
        | EventType.SubAgentFileWritten | EventType.SubAgentToolCall
  }>,
): void {
  const sa = subAgents.get(e.subAgentId);
  if (!sa) return;
  sa.events.push(e);
  if (e.type === EventType.SubAgentObservation) sa.totals.observations += 1;
  if (e.type === EventType.SubAgentFinding) sa.totals.findings += 1;
  if (e.type === EventType.SubAgentFileWritten) sa.totals.files += 1;
  if (e.type === EventType.SubAgentToolCall) sa.totals.tools += 1;
}

function ingestSubAgentCompleted(
  subAgents: Map<string, SubAgentBucket>,
  e: Extract<RunEvent, { type: EventType.SubAgentCompleted }>,
): void {
  const sa = subAgents.get(e.subAgentId);
  if (!sa) return;
  sa.endMs = Date.parse(e.timestamp);
  sa.status = statusFromString(e.status);
}

function ingestSandboxCommand(
  steps: Map<number, StepBucket>, activeStepIndex: number,
  e: Extract<RunEvent, { type: EventType.SandboxCommand }>,
): void {
  const bucket = steps.get(activeStepIndex);
  if (!bucket) return;
  const prev = bucket.sandboxRepos.get(e.repo);
  bucket.sandboxRepos.set(e.repo, {
    repo: e.repo, command: e.command, commandSummary: e.summary,
    exitCode: prev?.exitCode ?? null, durationMs: prev?.durationMs ?? null,
  });
  // p0228: keep the full chronological sequence, not just last-per-repo, so
  // the run-detail can show in order what the agent did + what it searched.
  bucket.commands.push({
    repo: e.repo, verb: e.command, summary: e.summary,
    exitCode: null, durationMs: null, timestamp: e.timestamp,
  });
}

function ingestSandboxResult(
  steps: Map<number, StepBucket>, activeStepIndex: number,
  e: Extract<RunEvent, { type: EventType.SandboxResult }>,
): void {
  const bucket = steps.get(activeStepIndex);
  if (!bucket) return;
  const prev = bucket.sandboxRepos.get(e.repo);
  bucket.sandboxRepos.set(e.repo, {
    repo: e.repo, command: prev?.command ?? e.command,
    commandSummary: prev?.commandSummary ?? null,
    exitCode: e.exitCode, durationMs: e.durationMs,
  });
  // p0228: fill the matching open command entry (last for this repo without
  // a result yet) with its outcome.
  for (let i = bucket.commands.length - 1; i >= 0; i--) {
    const c = bucket.commands[i];
    if (c.repo === e.repo && c.exitCode === null && c.durationMs === null) {
      c.exitCode = e.exitCode;
      c.durationMs = e.durationMs;
      break;
    }
  }
}

function groupSubAgentsByStep(
  subAgents: Map<string, SubAgentBucket>,
): Map<number, SubAgentBucket[]> {
  const out = new Map<number, SubAgentBucket[]>();
  for (const sa of subAgents.values()) {
    const list = out.get(sa.spawnStepIndex) ?? [];
    list.push(sa);
    out.set(sa.spawnStepIndex, list);
  }
  return out;
}

function stepBucketToNode(
  s: StepBucket, runStartMs: number, nowMs: number, totalSeconds: number,
  subAgents: SubAgentBucket[], runId: string | null, runEnded: boolean,
): ExecutionNodeProps {
  const startSec = (s.startMs - runStartMs) / 1000;
  const endSec = ((s.endMs ?? nowMs) - runStartMs) / 1000;
  const durationSec = Math.max(0, endSec - startSec);
  const llm = pairLlmCalls(s.events);
  const tail = pickStepTail(s);
  const body = composeStepBody(s, runId, llm.pairs, runEnded);
  const children = subAgents.map((sa) => subAgentBucketToNode(sa, runStartMs, nowMs, totalSeconds));
  return {
    id: `step-${s.index}`,
    label: s.displayName ?? s.name,
    status: s.status,
    depth: 0,
    startSeconds: Math.max(0, startSec),
    durationSeconds: durationSec,
    totalSeconds,
    durationLabel: formatDuration(durationSec),
    tail, body, children,
    message: s.message ?? null,
    costBadge: composeCostBadge(llm.totalCostUsd, llm.callCount),
  };
}

function composeStepBody(
  s: StepBucket, runId: string | null, pairs: ReadonlyArray<PairedLlmCall>,
  runEnded: boolean,
): React.ReactNode | null {
  const drawerEvents = mapToDrawerEvents(
    s.events.filter((e) => e.type !== EventType.LlmCallStarted && e.type !== EventType.LlmCallFinished),
  );
  const hasTicketEvent = s.events.some((e) => e.type === EventType.TicketFetched);
  const hasCatalogEvent = s.events.some((e) => e.type === EventType.CatalogLoaded);
  const sandboxRepos = [...s.sandboxRepos.values()];
  const sandboxBody = sandboxRepos.length > 0 && runId
    ? <StepSandboxes runId={runId} sandboxes={sandboxRepos} /> : null;
  // p0223: the commit/PR step's meaningful per-repo outcome, rendered above the
  // raw sandbox rows so "no changes" reads neutral and the PR is a link.
  const prOutcomes = s.events.filter(
    (e): e is Extract<RunEvent, { type: EventType.PullRequestOutcome }> =>
      e.type === EventType.PullRequestOutcome,
  );
  const prOutcomeBody = prOutcomes.length > 0 ? <PrOutcomeList events={prOutcomes} /> : null;
  // p0228: when a step ran more than one command per repo (real exploration —
  // the analyzer/master read/grep/find sequence), show the chronological
  // action timeline so the operator sees in order WHAT the agent did and what
  // it searched. For a one-command-per-repo step (a build/test) the per-repo
  // sandbox box below already says it, so the timeline would just be noise.
  const commandBody = s.commands.length > s.sandboxRepos.size
    ? <CommandTimeline commands={s.commands} /> : null;
  const llmBody = pairs.length > 0 ? <LlmCallsBody calls={pairs} runEnded={runEnded} /> : null;
  const primaryBody = hasCatalogEvent
    ? <CatalogLoadBody events={s.events} />
    : hasTicketEvent
    ? <FetchTicketBody events={s.events} />
    : drawerEvents.length > 0 ? <EventDrawer events={drawerEvents} /> : null;
  const parts: Array<{ key: string; node: React.ReactElement }> = [];
  if (prOutcomeBody) parts.push({ key: "pr-outcomes", node: prOutcomeBody });
  if (commandBody) parts.push({ key: "commands", node: commandBody });
  if (sandboxBody) parts.push({ key: "sandboxes", node: sandboxBody });
  if (llmBody) parts.push({ key: "llm", node: llmBody });
  if (primaryBody) parts.push({ key: "primary", node: primaryBody });
  if (parts.length === 0) return null;
  return <>{parts.map((p) => <div key={p.key}>{p.node}</div>)}</>;
}

function composeCostBadge(totalUsd: number, callCount: number): string | null {
  if (callCount === 0) return null;
  return `$${totalUsd.toFixed(4)} · ${callCount} LLM`;
}

function subAgentBucketToNode(
  sa: SubAgentBucket, runStartMs: number, nowMs: number, totalSeconds: number,
): ExecutionNodeProps {
  const startSec = (sa.startMs - runStartMs) / 1000;
  const endSec = ((sa.endMs ?? nowMs) - runStartMs) / 1000;
  const durationSec = Math.max(0, endSec - startSec);
  const drawerEvents = mapToDrawerEvents(sa.events);
  return {
    id: `sub-${sa.id}`,
    label: `sub-agent: ${sa.name}`,
    labelMono: true,
    status: sa.status, depth: 1,
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
  return { text: describeEvent(last), timestamp: shortTime(last.timestamp) };
}

function pickStepTail(s: StepBucket): { text: string; timestamp: string } | undefined {
  const fromEvents = pickTail(s.events);
  if (fromEvents !== undefined) return fromEvents;
  const ts = formatHms(s.endMs ?? s.startMs);
  if (s.status === "ok") return { text: s.message ?? "done", timestamp: ts };
  if (s.status === "fail") return { text: s.message ?? "failed", timestamp: ts };
  if (s.status === "run") return { text: "running…", timestamp: ts };
  return undefined;
}

function collapseMultiRepoSiblings(
  nodes: ExecutionNodeProps[], buckets: StepBucket[],
): ExecutionNodeProps[] {
  const out: ExecutionNodeProps[] = [];
  let i = 0;
  while (i < nodes.length) {
    const baseCmd = extractBaseCommand(buckets[i].name);
    if (!isMultiRepoCommand(baseCmd)) { out.push(nodes[i]); i += 1; continue; }
    let j = i + 1;
    while (j < nodes.length && extractBaseCommand(buckets[j].name) === baseCmd) j += 1;
    if (j - i < 2) { out.push(nodes[i]); i += 1; continue; }
    out.push(buildAggregateNode(nodes.slice(i, j), buckets.slice(i, j), baseCmd));
    i = j;
  }
  return out;
}

function buildAggregateNode(
  childNodes: ExecutionNodeProps[], childBuckets: StepBucket[], baseCommand: string,
): ExecutionNodeProps {
  const rollup = buildRepoRollup(
    baseCommand,
    childBuckets[0].displayName?.replace(/\s*\([^)]*\)\s*$/, "") ?? baseCommand,
    childBuckets.map((b) => ({ stepName: b.name, status: b.status })),
  );
  const first = childNodes[0];
  const last = childNodes[childNodes.length - 1];
  const start = first.startSeconds;
  const end = last.startSeconds + last.durationSeconds;
  const aggStatus = rollup.failCount > 0 ? "fail" : "ok";
  return composeAggregateNode(rollup, childNodes, baseCommand, start, end - start, first.totalSeconds, aggStatus);
}

function composeAggregateNode(
  rollup: RepoRollup, childNodes: ExecutionNodeProps[], baseCommand: string,
  startSeconds: number, durationSeconds: number, totalSeconds: number,
  status: "ok" | "fail",
): ExecutionNodeProps {
  // p0203: aggregate cost = sum of children costs (parsed back from the
  // cost badge; cheap since we own the format). Keeps the parent honest.
  const costSum = childNodes.reduce((acc, n) => acc + parseCostFromBadge(n.costBadge), 0);
  const llmCount = childNodes.reduce((acc, n) => acc + parseCallsFromBadge(n.costBadge), 0);
  return {
    id: `step-agg-${baseCommand}-${startSeconds.toFixed(3)}`,
    label: rollup.baseDisplay,
    status, depth: 0,
    startSeconds, durationSeconds, totalSeconds,
    durationLabel: formatDuration(durationSeconds),
    children: childNodes.map((c) => ({ ...c, depth: 1 })),
    body: undefined,
    repoSummary: { text: rollup.summaryText, tone: rollup.tone },
    costBadge: llmCount > 0 ? `$${costSum.toFixed(4)} · ${llmCount} LLM` : null,
    // p0202 TODO: when PersistWorkBranchHandler emits the typed outcome
    // split (Success / NothingToCommit / Failed), refine the parent
    // message to read "5/5 persisted (4 with WIP commit, 1 nothing to
    // commit)" instead of the generic N/M ok summary. Until p0202
    // merges we render the existing buckets' shape (fail vs ok only).
    message: null,
  };
}

function parseCostFromBadge(badge: string | null | undefined): number {
  if (!badge) return 0;
  const m = badge.match(/^\$([0-9.]+)/);
  return m ? parseFloat(m[1]) : 0;
}

function parseCallsFromBadge(badge: string | null | undefined): number {
  if (!badge) return 0;
  const m = badge.match(/·\s*(\d+)\s*LLM/);
  return m ? parseInt(m[1], 10) : 0;
}
