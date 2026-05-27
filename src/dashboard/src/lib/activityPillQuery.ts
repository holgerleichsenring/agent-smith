import { EventType } from "@/types/hub-events";

// p0169j-b: operator-vocabulary pill groups for the Activity tab.
// Six categories on top of the EventType contract; L1 lifecycle events
// (RunStarted/RunFinished/StepStarted/StepFinished) are the always-visible
// chronological scaffold and are NOT a pill — they render regardless of
// pill state.

export type ActivityPill =
  | "decisions"
  | "tools"
  | "llm"
  | "sandbox"
  | "gates"
  | "issues";

export const ALL_PILLS: readonly ActivityPill[] = [
  "decisions",
  "tools",
  "llm",
  "sandbox",
  "gates",
  "issues",
];

export const LIFECYCLE_TYPES: ReadonlySet<EventType> = new Set([
  EventType.RunStarted,
  EventType.RunFinished,
  EventType.StepStarted,
  EventType.StepFinished,
]);

const PILL_TYPES: Record<ActivityPill, ReadonlySet<EventType>> = {
  decisions: new Set([EventType.TriageRoute, EventType.DecisionLogged]),
  tools: new Set([EventType.ToolCall, EventType.ToolResult]),
  llm: new Set([EventType.LlmCallStarted, EventType.LlmCallFinished]),
  sandbox: new Set([
    EventType.SandboxCreated,
    EventType.SandboxDisposed,
    EventType.SandboxCommand,
    EventType.SandboxOutput,
    EventType.SandboxResult,
  ]),
  gates: new Set([EventType.GateChecked]),
  issues: new Set([EventType.CatalogIssue]),
};

export function pillForEvent(type: EventType): ActivityPill | null {
  for (const pill of ALL_PILLS) {
    if (PILL_TYPES[pill].has(type)) return pill;
  }
  return null;
}

export function defaultPillState(): ReadonlySet<ActivityPill> {
  return new Set(ALL_PILLS);
}

export function parsePillsFromQuery(params: URLSearchParams): ReadonlySet<ActivityPill> {
  const raw = params.get("activity");
  if (raw === null) return defaultPillState();
  if (raw.trim() === "") return new Set();
  const pills = new Set<ActivityPill>();
  for (const token of raw.split(",")) {
    const trimmed = token.trim().toLowerCase();
    if (ALL_PILLS.includes(trimmed as ActivityPill)) {
      pills.add(trimmed as ActivityPill);
    }
  }
  return pills;
}

export function writePillsToQuery(
  pills: ReadonlySet<ActivityPill>,
  base: URLSearchParams,
): URLSearchParams {
  const params = new URLSearchParams(base);
  const isDefault =
    pills.size === ALL_PILLS.length && ALL_PILLS.every((p) => pills.has(p));
  if (isDefault) {
    params.delete("activity");
    return params;
  }
  const ordered = ALL_PILLS.filter((p) => pills.has(p));
  // Empty string distinguishes "all-off" from "default" (the latter
  // omits the param entirely).
  params.set("activity", ordered.join(","));
  return params;
}

export function isEventVisible(
  type: EventType,
  pills: ReadonlySet<ActivityPill>,
): boolean {
  if (LIFECYCLE_TYPES.has(type)) return true;
  const pill = pillForEvent(type);
  if (pill === null) return false;
  return pills.has(pill);
}
