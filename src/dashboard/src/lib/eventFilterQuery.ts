import { EventType } from "@/types/hub-events";

// p0169g: URL-search-string round-trip for the FilterRail. Defaults
// are encoded by absence — only non-default toggles write the param.
// L1 + L2 are on by default; L3 is off (mirrors the hub's gated fanout).

export type EventLevel = "L1" | "L2" | "L3";

export interface EventFilterState {
  l1: ReadonlySet<EventType>;
  l2: ReadonlySet<EventType>;
  l3: ReadonlySet<EventType>;
}

export const L1_TYPES: ReadonlySet<EventType> = new Set([
  EventType.RunStarted, EventType.RunFinished,
  EventType.SandboxCreated, EventType.SandboxDisposed,
  EventType.StepStarted, EventType.StepFinished,
  EventType.L1StepDetail,
]);

export const L2_TYPES: ReadonlySet<EventType> = new Set([
  EventType.DecisionLogged, EventType.GateChecked, EventType.TriageRoute,
  EventType.LlmCallStarted, EventType.LlmCallFinished,
]);

export const L3_TYPES: ReadonlySet<EventType> = new Set([
  EventType.SandboxCommand, EventType.SandboxOutput, EventType.SandboxResult,
  EventType.ToolCall, EventType.ToolResult,
]);

export function defaultFilterState(): EventFilterState {
  return {
    l1: new Set(L1_TYPES),
    l2: new Set(L2_TYPES),
    l3: new Set(),
  };
}

const TYPE_KEYS: Record<string, EventType> = {
  RunStarted: EventType.RunStarted,
  RunFinished: EventType.RunFinished,
  SandboxCreated: EventType.SandboxCreated,
  SandboxDisposed: EventType.SandboxDisposed,
  StepStarted: EventType.StepStarted,
  StepFinished: EventType.StepFinished,
  DecisionLogged: EventType.DecisionLogged,
  GateChecked: EventType.GateChecked,
  TriageRoute: EventType.TriageRoute,
  LlmCallStarted: EventType.LlmCallStarted,
  LlmCallFinished: EventType.LlmCallFinished,
  SandboxCommand: EventType.SandboxCommand,
  SandboxOutput: EventType.SandboxOutput,
  SandboxResult: EventType.SandboxResult,
  ToolCall: EventType.ToolCall,
  ToolResult: EventType.ToolResult,
  L1StepDetail: EventType.L1StepDetail,
  CatalogIssue: EventType.CatalogIssue,
};

const KEY_BY_TYPE: Record<EventType, string> = Object.fromEntries(
  Object.entries(TYPE_KEYS).map(([k, v]) => [v, k]),
) as Record<EventType, string>;

export function parseFilterFromQuery(params: URLSearchParams): EventFilterState {
  const state = defaultFilterState();
  apply(params.get("l1"), L1_TYPES, state.l1 as Set<EventType>);
  apply(params.get("l2"), L2_TYPES, state.l2 as Set<EventType>);
  apply(params.get("l3"), L3_TYPES, state.l3 as Set<EventType>);
  return state;
}

function apply(raw: string | null, allowed: ReadonlySet<EventType>, target: Set<EventType>): void {
  if (raw === null) return;
  target.clear();
  if (raw.trim() === "") return;
  for (const token of raw.split(",")) {
    const trimmed = token.trim();
    if (!trimmed) continue;
    const t = TYPE_KEYS[trimmed];
    if (t !== undefined && allowed.has(t)) target.add(t);
  }
}

// Per-level defaults: L1 + L2 default to "all on", L3 to "off". The
// URL param is written only when the current state diverges from THAT
// level's default — not from the level's full type set.
const L3_DEFAULT: ReadonlySet<EventType> = new Set();

export function writeFilterToQuery(
  state: EventFilterState, base: URLSearchParams,
): URLSearchParams {
  const params = new URLSearchParams(base);
  serialise("l1", state.l1, L1_TYPES, params);
  serialise("l2", state.l2, L2_TYPES, params);
  serialise("l3", state.l3, L3_DEFAULT, params);
  return params;
}

function serialise(
  key: string,
  current: ReadonlySet<EventType>,
  defaults: ReadonlySet<EventType>,
  params: URLSearchParams,
): void {
  const sameAsDefault = current.size === defaults.size
    && [...current].every((t) => defaults.has(t));
  if (sameAsDefault) {
    params.delete(key);
    return;
  }
  const ordered = [...current]
    .sort((a, b) => a - b)
    .map((t) => KEY_BY_TYPE[t])
    .filter(Boolean);
  // The empty string is meaningful — "this level is explicitly empty"
  // distinguishes "off" from "default".
  params.set(key, ordered.join(","));
}

export function isAllowed(state: EventFilterState, type: EventType): boolean {
  if (L1_TYPES.has(type)) return state.l1.has(type);
  if (L2_TYPES.has(type)) return state.l2.has(type);
  if (L3_TYPES.has(type)) return state.l3.has(type);
  return false;
}
