import { describe, expect, it } from "vitest";
import { EventType } from "@/types/hub-events";
import {
  ALL_PILLS,
  defaultPillState,
  isEventVisible,
  parsePillsFromQuery,
  pillForEvent,
  writePillsToQuery,
} from "../activityPillQuery";

describe("activityPillQuery", () => {
  it("defaultPillState contains all six pills", () => {
    const state = defaultPillState();
    expect(state.size).toBe(6);
    for (const pill of ALL_PILLS) expect(state.has(pill)).toBe(true);
  });

  it("parsePillsFromQuery — missing param returns default (all on)", () => {
    const params = new URLSearchParams();
    const state = parsePillsFromQuery(params);
    expect(state.size).toBe(6);
  });

  it("parsePillsFromQuery — empty string means all-off", () => {
    const params = new URLSearchParams("activity=");
    const state = parsePillsFromQuery(params);
    expect(state.size).toBe(0);
  });

  it("parsePillsFromQuery — comma list selects exactly those pills", () => {
    const params = new URLSearchParams("activity=decisions,tools");
    const state = parsePillsFromQuery(params);
    expect(state.size).toBe(2);
    expect(state.has("decisions")).toBe(true);
    expect(state.has("tools")).toBe(true);
  });

  it("parsePillsFromQuery — unknown tokens are dropped", () => {
    const params = new URLSearchParams("activity=decisions,nonsense,tools");
    const state = parsePillsFromQuery(params);
    expect(state.size).toBe(2);
  });

  it("writePillsToQuery — default state omits the param", () => {
    const base = new URLSearchParams("tab=activity");
    const out = writePillsToQuery(defaultPillState(), base);
    expect(out.get("activity")).toBeNull();
    expect(out.get("tab")).toBe("activity");
  });

  it("writePillsToQuery — empty set writes empty string", () => {
    const out = writePillsToQuery(new Set(), new URLSearchParams());
    expect(out.get("activity")).toBe("");
  });

  it("writePillsToQuery — partial set writes comma list in declared order", () => {
    const out = writePillsToQuery(
      new Set(["tools", "decisions"]),
      new URLSearchParams(),
    );
    expect(out.get("activity")).toBe("decisions,tools");
  });

  it("pillForEvent — CatalogIssue maps to issues", () => {
    expect(pillForEvent(EventType.CatalogIssue)).toBe("issues");
  });

  it("pillForEvent — lifecycle events have no pill", () => {
    expect(pillForEvent(EventType.RunStarted)).toBeNull();
    expect(pillForEvent(EventType.StepFinished)).toBeNull();
  });

  it("isEventVisible — lifecycle events are always visible regardless of pills", () => {
    const noPills = new Set();
    expect(isEventVisible(EventType.RunStarted, noPills)).toBe(true);
    expect(isEventVisible(EventType.StepFinished, noPills)).toBe(true);
  });

  it("isEventVisible — issues pill hides CatalogIssue when off", () => {
    const withoutIssues = new Set(ALL_PILLS.filter((p) => p !== "issues"));
    expect(isEventVisible(EventType.CatalogIssue, withoutIssues)).toBe(false);
  });

  it("isEventVisible — issues pill shows CatalogIssue when on", () => {
    const onlyIssues = new Set(["issues"] as const);
    expect(isEventVisible(EventType.CatalogIssue, onlyIssues)).toBe(true);
  });
});
