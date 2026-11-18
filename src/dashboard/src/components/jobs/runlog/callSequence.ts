import { callActivity, pairLlmCalls } from "@/hooks/execution-tree/llmPairing";
import { EventType, type RunEvent } from "@/types/hub-events";
import { shortMs } from "./stepDetailLines";

// 2026-11-18-e06be: a step's model calls as a numbered sequence, oldest first. Each call carries the event-derived
// fallbacks for its two dim lines: fed, the tool results that came back since the call before it, and decided, the
// tool calls it made before the next one began. The prompt store's digest replaces them where it holds the call.

export interface SequenceCall {
  key: string;
  /** The call's number in its step, from 1; null when older calls are not loaded and the step's count is unknown. */
  n: number | null;
  activity: string;
  model: string;
  tokensIn: number | null;
  cachedIn: number | null;
  tokensOut: number | null;
  costUsd: number | null;
  durationMs: number | null;
  finished: boolean;
  promptId: string | null;
  fed: string | null;
  decided: string | null;
}

function between(events: RunEvent[], from: string | null, to: string | null): RunEvent[] {
  return events.filter((e) => (from === null || e.timestamp >= from) && (to === null || e.timestamp < to));
}

function fedFallback(events: RunEvent[]): string | null {
  const words: string[] = [];
  for (const e of events) {
    if (e.type !== EventType.ToolResult) continue;
    words.push(e.ok ? `${e.tool} result, ${e.resultLength.toLocaleString("en-US")} chars` : `${e.tool} failed${e.errorMessage ? `: ${e.errorMessage}` : ""}`);
  }
  if (words.length === 0) return null;
  return words.length > 1 ? `${words[words.length - 1]} (+${words.length - 1} more)` : words[0];
}

function decidedFallback(events: RunEvent[]): string | null {
  const words: string[] = [];
  for (const e of events) if (e.type === EventType.ToolCall) words.push(`${e.tool}${e.summary ? ` ${e.summary}` : ""}`);
  return words.length > 0 ? words.join("; ") : null;
}

/** @param total the step's llm call count; @param complete every event of the step is loaded. */
export function callSequence(events: RunEvent[], total: number | null, complete: boolean): SequenceCall[] {
  const pairs = [...pairLlmCalls(events).pairs].sort((l, r) => l.startedAt.localeCompare(r.startedAt));
  const number = (i: number): number | null => {
    if (complete) return i + 1;
    return total !== null && total >= pairs.length ? total - (pairs.length - 1 - i) : null;
  };
  return pairs.map((p, i) => {
    const previous = i > 0 ? pairs[i - 1] : null;
    const next = pairs[i + 1] ?? null;
    return {
      key: p.id, n: number(i), activity: callActivity(p), model: p.model,
      tokensIn: p.tokensIn, cachedIn: p.cachedTokensIn, tokensOut: p.tokensOut, costUsd: p.costUsd, durationMs: p.durationMs,
      finished: p.finishedAt !== null, promptId: p.promptId,
      fed: fedFallback(between(events, previous?.finishedAt ?? null, p.startedAt)),
      decided: p.finishedAt === null ? null : decidedFallback(between(events, p.finishedAt, next?.startedAt ?? null)),
    };
  });
}

function count(n: number | null): string {
  return n === null ? "?" : n.toLocaleString("en-US");
}

/** "12,400 in (9,800 cache) / 410 out". */
export function tokenWords(c: SequenceCall): string {
  const cache = c.cachedIn ? ` (${count(c.cachedIn)} cache)` : "";
  return `${count(c.tokensIn)} in${cache} / ${count(c.tokensOut)} out`;
}

/** "$0.031 · 4.2s", or "in flight" / "ended" for a call with no finish. */
export function spendWords(c: SequenceCall, runEnded: boolean): string {
  if (!c.finished) return runEnded ? "ended" : "in flight";
  return [c.costUsd !== null ? `$${c.costUsd.toFixed(3)}` : "", shortMs(c.durationMs)].filter(Boolean).join(" · ");
}
