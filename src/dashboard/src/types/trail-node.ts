import type { RunEvent, EventType } from "./hub-events";

// p0169h: client-side projection of a RunEvent[] into a navigable tree.
// Pure data — built by TrailAssembler from the events array, consumed by
// TrailTree + TrailNodeDetailPane.

export type TrailNodeKind = "run" | "step" | "skill-call" | "tool-pair" | "decision" | "triage";

export interface TrailNode {
  id: string;
  kind: TrailNodeKind;
  label: string;
  startedAtMs: number;
  durationMs: number | null;
  payload: RunEvent | RunEvent[] | null;
  eventTypes: Set<EventType>;
  /** GateChecked events attached as inline chips on this node, not as children. */
  gateChips: GateChip[];
  children: TrailNode[];
}

export interface GateChip {
  gate: string;
  passed: boolean;
  reason: string;
}
