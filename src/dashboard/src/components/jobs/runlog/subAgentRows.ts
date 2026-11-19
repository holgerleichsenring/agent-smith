import { pairLlmCalls } from "@/hooks/execution-tree/llmPairing";
import { EventType, type RunEvent } from "@/types/hub-events";
import { clockOf } from "./runNowLines";
import { childEnd, shortMs } from "./stepDetailLines";

// 2026-11-19-687eb: an open step's sub-agents, one row each — who it is, what its master asked of it, whether it is
// still at it, how many tool calls it made and what they were. Read from the step's events: the lifecycle (Spawned,
// Completed, Messaged) is always on the step's newest page; the tool calls and LLM calls are paged with the rest.

export interface SubAgentCall {
  key: string;
  ts: string;
  at: string;
  tool: string;
  target: string;
}

export interface SubAgentRow {
  id: string;
  name: string;
  /** What its master asked of it — the Spawned event's activity. */
  brief: string;
  running: boolean;
  /** "finished", "failed", "stopped …" in the log's words; "running" while it works. */
  status: string;
  failed: boolean;
  startedAt: string;
  endedAt: string | null;
  calls: SubAgentCall[];
  /** 2026-11-19-1b72d: the tool calls its Completed events recorded, summed over its passes; 0 before the first. */
  recordedCalls: number;
  /** The recorded count covers every pass: each pass (the first and each continuation loaded here) has its Completed
   *  event, and none ended Failed or Stopped — those record no calls. */
  recordedComplete: boolean;
  /** Older events of the step are not loaded and the child started before them, so its calls are a lower bound. */
  partial: boolean;
  /** When its open LLM call started, while it runs and one is open. */
  thinkingSince: string | null;
}

const LIFECYCLE = new Set<EventType>([EventType.SubAgentSpawned, EventType.SubAgentCompleted, EventType.SubAgentMessaged]);

export function subAgentRows(events: RunEvent[], opts: { runEnded: boolean; hasOlder: boolean }): SubAgentRow[] {
  const ordered = [...events].sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  const rows = new Map<string, SubAgentRow>();
  const passes = new Map<string, { passes: number; completed: number; uncounted: boolean }>();
  for (const e of ordered) {
    if (e.type === EventType.SubAgentSpawned) {
      if (!rows.has(e.subAgentId)) {
        rows.set(e.subAgentId, { id: e.subAgentId, name: e.name, brief: e.activity, running: true, status: "running", failed: false,
          startedAt: e.timestamp, endedAt: null, calls: [], recordedCalls: 0, recordedComplete: false, partial: false, thinkingSince: null });
        passes.set(e.subAgentId, { passes: 1, completed: 0, uncounted: false });
      }
      continue;
    }
    if (e.type !== EventType.SubAgentCompleted && e.type !== EventType.SubAgentMessaged && e.type !== EventType.SubAgentToolCall) continue;
    const row = rows.get(e.subAgentId);
    if (!row) continue;
    if (e.type === EventType.SubAgentCompleted) {
      Object.assign(row, { running: false, status: childEnd(e.status), failed: e.status === "Failed", endedAt: e.timestamp });
      row.recordedCalls += e.toolCalls ?? 0;
      const tally = passes.get(e.subAgentId)!;
      tally.completed += 1;
      if (e.status === "Failed" || e.status === "Stopped") tally.uncounted = true;
    } else if (e.type === EventType.SubAgentMessaged) {
      if (e.continued) {
        Object.assign(row, { running: true, status: "running", failed: false, endedAt: null });
        passes.get(e.subAgentId)!.passes += 1;
      }
    } else {
      row.calls.push({ key: `${row.calls.length}`, ts: e.timestamp, at: clockOf(e.timestamp) ?? "", tool: e.toolName, target: e.argsSummary ?? "" });
    }
  }

  const paged = events.filter((e) => !LIFECYCLE.has(e.type)).map((e) => e.timestamp).sort();
  const oldestPaged = paged[0] ?? null;
  const calls = pairLlmCalls(events).pairs;
  for (const row of rows.values()) {
    const tally = passes.get(row.id)!;
    row.recordedComplete = row.endedAt !== null && !tally.uncounted && tally.completed >= tally.passes;
    row.partial = opts.hasOlder && (oldestPaged === null || row.startedAt < oldestPaged);
    // A run that ended while a child had not said it was done: the child ended with it.
    if (opts.runEnded && row.running) Object.assign(row, { running: false, status: "ended with the run" });
    if (!row.running) continue;
    // Same-named children share a role, so they share this answer: the newest call any of them has open.
    const latest = calls.filter((c) => c.role === row.name).sort((a, b) => a.startedAt.localeCompare(b.startedAt)).at(-1);
    row.thinkingSince = latest && latest.finishedAt === null ? latest.startedAt : null;
  }
  return [...rows.values()];
}

/** "39 tool calls", "1 tool call", "12+ tool calls". 2026-11-19-1b72d: the count its Completed events recorded, or
 *  the calls on the page when more are loaded; a "+" while older calls are not loaded and the record does not cover
 *  every pass (a child at work, one ended with the run, a failed or stopped pass, a pass whose end is not loaded). */
export function callCount(row: SubAgentRow): string {
  const lowerBound = row.partial && !row.recordedComplete;
  const n = Math.max(row.calls.length, row.recordedCalls);
  return `${n}${lowerBound ? "+" : ""} ${n === 1 && !lowerBound ? "tool call" : "tool calls"}`;
}

/** "finished · 39 tool calls · 1m 40s"; a running child's line leaves the time to the live counter. */
export function rowSummary(row: SubAgentRow): string {
  const took = row.endedAt ? shortMs(Date.parse(row.endedAt) - Date.parse(row.startedAt)) : "";
  return [row.status, callCount(row), took].filter(Boolean).join(" · ");
}
