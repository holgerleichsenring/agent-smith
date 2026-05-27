import { describe, it, expect } from "vitest";
import {
  defaultFilterState,
  parseFilterFromQuery,
  writeFilterToQuery,
  isAllowed,
  L1_TYPES,
  L2_TYPES,
  L3_TYPES,
} from "../eventFilterQuery";
import { EventType } from "@/types/hub-events";

describe("eventFilterQuery", () => {
  it("defaults: L1 + L2 on, L3 off", () => {
    const state = defaultFilterState();
    expect(state.l1.size).toBe(L1_TYPES.size);
    expect(state.l2.size).toBe(L2_TYPES.size);
    expect(state.l3.size).toBe(0);
  });

  it("absent query params yield defaults", () => {
    const state = parseFilterFromQuery(new URLSearchParams());
    expect([...state.l3]).toEqual([]);
    expect([...state.l1].length).toBe(L1_TYPES.size);
  });

  it("explicit l3 param turns it on", () => {
    const state = parseFilterFromQuery(new URLSearchParams("l3=ToolCall,ToolResult"));
    expect(state.l3.has(EventType.ToolCall)).toBe(true);
    expect(state.l3.has(EventType.ToolResult)).toBe(true);
    expect(state.l3.has(EventType.SandboxOutput)).toBe(false);
  });

  it("writes l3 param when l3 is non-default", () => {
    const state = defaultFilterState();
    state.l3 = new Set([EventType.ToolCall]);
    const params = writeFilterToQuery(state, new URLSearchParams());
    expect(params.get("l3")).toBe("ToolCall");
    expect(params.has("l1")).toBe(false);
    expect(params.has("l2")).toBe(false);
  });

  it("clears l3 param when l3 returns to default empty", () => {
    const state = defaultFilterState();
    const params = writeFilterToQuery(state, new URLSearchParams("l3=ToolCall"));
    expect(params.has("l3")).toBe(false);
  });

  it("isAllowed mirrors the level membership", () => {
    const state = defaultFilterState();
    expect(isAllowed(state, EventType.RunStarted)).toBe(true);
    expect(isAllowed(state, EventType.DecisionLogged)).toBe(true);
    expect(isAllowed(state, EventType.ToolCall)).toBe(false);
    state.l3 = new Set([EventType.ToolCall, EventType.ToolResult, ...L3_TYPES]);
    expect(isAllowed({ ...state, l3: new Set([EventType.ToolCall]) }, EventType.ToolCall)).toBe(true);
  });

  it("round-trips a partial l3 set", () => {
    const original = parseFilterFromQuery(new URLSearchParams("l3=ToolCall"));
    const params = writeFilterToQuery(original, new URLSearchParams("l3=ToolCall"));
    const reparsed = parseFilterFromQuery(params);
    expect([...reparsed.l3]).toEqual([...original.l3]);
  });
});
