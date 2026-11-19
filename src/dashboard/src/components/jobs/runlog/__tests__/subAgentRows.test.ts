import { describe, expect, it } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import { rowSummary, subAgentRows } from "../subAgentRows";

// 2026-11-19-687eb: an open step's sub-agents, one row each, read from the step's events.

const ev = (o: Record<string, unknown>): RunEvent => ({ runId: "r1", ...o }) as unknown as RunEvent;
const at = (s: number) => `2026-11-19T10:${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}Z`;
const spawned = (id: string, name: string, s: number) =>
  ev({ type: EventType.SubAgentSpawned, timestamp: at(s), subAgentId: id, name, activity: `brief of ${name}`, parentSubAgentId: null, inheritedContextHash: "x" });
const completed = (id: string, status: string, s: number) =>
  ev({ type: EventType.SubAgentCompleted, timestamp: at(s), subAgentId: id, status, observationsCount: 0, findingsCount: 0, filesWrittenCount: 0, toolCalls: 1, costUsd: 0 });
const call = (id: string, tool: string, target: string, s: number) =>
  ev({ type: EventType.SubAgentToolCall, timestamp: at(s), subAgentId: id, toolName: tool, argsSummary: target });
const llmStarted = (role: string, s: number) =>
  ev({ type: EventType.LlmCallStarted, timestamp: at(s), model: "gpt-x", role, promptHash: "h" });
const llmFinished = (role: string, s: number) =>
  ev({ type: EventType.LlmCallFinished, timestamp: at(s), model: "gpt-x", role, tokensIn: 1, tokensOut: 1, costUsd: 0.01, durationMs: 1000 });

describe("subAgentRows", () => {
  it("a finished child counts its recorded calls and shows how long it took", () => {
    const rows = subAgentRows([
      spawned("a", "PoC capability analyst", 0), call("a", "read_file", "src/a.cs", 10), call("a", "grep", "Store", 20),
      completed("a", "Succeeded", 100),
    ], { runEnded: false, hasOlder: false });

    expect(rows).toHaveLength(1);
    expect(rowSummary(rows[0])).toBe("finished · 2 tool calls · 1m 40s");
    expect(rows[0].calls.map((c) => [c.tool, c.target])).toEqual([["read_file", "src/a.cs"], ["grep", "Store"]]);
    expect(rows[0].brief).toBe("brief of PoC capability analyst");
  });

  it("a child sent back after it finished is running again", () => {
    const rows = subAgentRows([
      spawned("a", "Builder", 0), completed("a", "Succeeded", 10),
      ev({ type: EventType.SubAgentMessaged, timestamp: at(11), subAgentId: "a", text: "the 404", continued: true }),
    ], { runEnded: false, hasOlder: false });

    expect(rows[0].running).toBe(true);
    expect(rowSummary(rows[0])).toBe("running · 0 tool calls");
  });

  it("a running child whose LLM call is open is thinking since it started; a closed call is not", () => {
    const rows = subAgentRows([
      spawned("a", "Analyst", 0), spawned("b", "Auditor", 0),
      llmStarted("Analyst", 5), llmFinished("Analyst", 9), call("a", "read_file", "x", 10), llmStarted("Analyst", 11),
      llmStarted("Auditor", 3), llmFinished("Auditor", 8),
    ], { runEnded: false, hasOlder: false });

    expect(rows.find((r) => r.name === "Analyst")!.thinkingSince).toBe(at(11));
    expect(rows.find((r) => r.name === "Auditor")!.thinkingSince).toBeNull();
  });

  it("a failed child reads failed, and one left running by an ended run ended with it", () => {
    const rows = subAgentRows([spawned("a", "A", 0), completed("a", "Failed", 4), spawned("b", "B", 1)], { runEnded: true, hasOlder: false });

    expect(rows[0]).toMatchObject({ status: "failed", failed: true });
    expect(rows[1]).toMatchObject({ status: "ended with the run", running: false, thinkingSince: null });
  });

  it("marks the count a lower bound while the step's older events are not loaded and the child started before them", () => {
    const rows = subAgentRows([spawned("a", "A", 0), call("a", "read_file", "x", 50), spawned("b", "B", 60), call("b", "grep", "y", 61)],
      { runEnded: false, hasOlder: true });

    expect(rowSummary(rows[0])).toBe("running · 1+ tool calls");
    expect(rowSummary(rows[1])).toBe("running · 1 tool call");
  });
});
