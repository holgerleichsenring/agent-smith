import type {
  RunEvent,
  ToolCallEvent,
  ToolResultEvent,
} from "@/types/hub-events";
import { EventType } from "@/types/hub-events";

// p0169g: pair ToolCall + ToolResult events by tool name + temporal
// proximity. Events don't carry a correlation id today; the 60s window
// is the good-enough heuristic for the UI. Concurrent same-name calls
// surface as standalone rows so the operator can still reconstruct the
// sequence.

const PAIR_WINDOW_MS = 60_000;

export type ToolRow =
  | { kind: "pair"; call: ToolCallEvent; result: ToolResultEvent }
  | { kind: "call-only"; call: ToolCallEvent }
  | { kind: "result-only"; result: ToolResultEvent };

export function pairToolEvents(events: RunEvent[]): ToolRow[] {
  const rows: ToolRow[] = [];
  const unpairedByName = new Map<string, ToolCallEvent[]>();
  for (const event of events) {
    if (event.type === EventType.ToolCall) {
      const call = event;
      const queue = unpairedByName.get(call.tool) ?? [];
      queue.push(call);
      unpairedByName.set(call.tool, queue);
      rows.push({ kind: "call-only", call });
      continue;
    }
    if (event.type === EventType.ToolResult) {
      const result = event;
      const queue = unpairedByName.get(result.tool);
      const resultMs = Date.parse(result.timestamp);
      if (queue && queue.length > 0) {
        const candidate = queue[queue.length - 1];
        const callMs = Date.parse(candidate.timestamp);
        if (!Number.isNaN(callMs) && !Number.isNaN(resultMs)
            && resultMs - callMs <= PAIR_WINDOW_MS) {
          queue.pop();
          if (queue.length === 0) unpairedByName.delete(result.tool);
          // Replace the latest call-only row for this call with a pair row.
          for (let i = rows.length - 1; i >= 0; i--) {
            const row = rows[i];
            if (row.kind === "call-only" && row.call === candidate) {
              rows[i] = { kind: "pair", call: candidate, result };
              break;
            }
          }
          continue;
        }
      }
      rows.push({ kind: "result-only", result });
    }
  }
  return rows;
}
