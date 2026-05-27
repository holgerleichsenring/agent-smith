import { describe, it, expect } from "vitest";
import { pairToolEvents } from "../ToolEventPairer";
import {
  EventType,
  type RunEvent,
  type ToolCallEvent,
  type ToolResultEvent,
} from "@/types/hub-events";

function call(tool: string, atMs: number, argsLength = 4): ToolCallEvent {
  return {
    type: EventType.ToolCall,
    runId: "r",
    timestamp: new Date(atMs).toISOString(),
    tool,
    argsLength,
  };
}

function result(tool: string, atMs: number, ok = true, resultLength = 8): ToolResultEvent {
  return {
    type: EventType.ToolResult,
    runId: "r",
    timestamp: new Date(atMs).toISOString(),
    tool,
    ok,
    resultLength,
  };
}

describe("ToolEventPairer", () => {
  it("pairs a call followed by a same-name result within 60s", () => {
    const events: RunEvent[] = [
      call("read_file", 1000),
      result("read_file", 1500),
    ];
    const rows = pairToolEvents(events);
    expect(rows).toHaveLength(1);
    expect(rows[0].kind).toBe("pair");
  });

  it("renders unpaired call as standalone when result arrives after 60s", () => {
    const events: RunEvent[] = [
      call("read_file", 1000),
      result("read_file", 1000 + 60_001),
    ];
    const rows = pairToolEvents(events);
    expect(rows).toHaveLength(2);
    expect(rows[0].kind).toBe("call-only");
    expect(rows[1].kind).toBe("result-only");
  });

  it("renders concurrent same-name calls as two standalone rows", () => {
    const events: RunEvent[] = [
      call("read_file", 1000),
      call("read_file", 1100),
      result("read_file", 1200),
      // second result never arrives during this fixture
    ];
    const rows = pairToolEvents(events);
    // newest call pairs with the result, oldest stays unpaired
    expect(rows).toHaveLength(2);
    expect(rows.some((r) => r.kind === "call-only")).toBe(true);
    expect(rows.some((r) => r.kind === "pair")).toBe(true);
  });

  it("preserves order when multiple distinct-name calls are paired", () => {
    const events: RunEvent[] = [
      call("read_file", 1000),
      result("read_file", 1100),
      call("write_file", 1200),
      result("write_file", 1300),
    ];
    const rows = pairToolEvents(events);
    expect(rows).toHaveLength(2);
    expect(rows.every((r) => r.kind === "pair")).toBe(true);
    if (rows[0].kind === "pair") expect(rows[0].call.tool).toBe("read_file");
    if (rows[1].kind === "pair") expect(rows[1].call.tool).toBe("write_file");
  });
});
