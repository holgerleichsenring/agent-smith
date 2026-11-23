import type { TextKey } from "@/lib/i18n/translate";

// 2026-11-23-933da: how a PoC's state reads — its word and its dot. The order is the filter pills' order. A state this
// dashboard does not know yet reads as itself with a quiet dot, never as a guess.

export type PocDot = "ok" | "run" | "bad" | "idle";

interface StateLook {
  label: TextKey;
  dot: PocDot;
}

export const POC_STATES: Readonly<Record<string, StateLook>> = {
  ready: { label: "pocs.state.ready", dot: "ok" },
  importing: { label: "pocs.state.importing", dot: "run" },
  blocked: { label: "pocs.state.blocked", dot: "bad" },
  failed: { label: "pocs.state.failed", dot: "bad" },
  refused: { label: "pocs.state.refused", dot: "bad" },
  stopped: { label: "pocs.state.stopped", dot: "idle" },
  deleted: { label: "pocs.state.deleted", dot: "idle" },
};

export function pocDot(state: string): PocDot {
  return POC_STATES[state]?.dot ?? "idle";
}

/** The states present in these rows, in pill order; unknown states last, as they came. */
export function statesPresent(states: string[]): string[] {
  const known = Object.keys(POC_STATES).filter((state) => states.includes(state));
  const unknown = [...new Set(states.filter((state) => !(state in POC_STATES)))];
  return [...known, ...unknown];
}
