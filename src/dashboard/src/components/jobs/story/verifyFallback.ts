import {
  EventType,
  type ExpectationRatifiedEvent,
  type RunEvent,
} from "@/types/hub-events";

// p0344b: the LEGACY verify view, built from the run's ExpectationRatified
// event (p0328). Used ONLY for runs persisted before snapshot.acceptance
// existed — a run carrying the persisted per-criterion dispositions renders
// those instead (VerifySummary). Pure module, no React.

export interface RatifiedExpectation {
  observed: string;
  expected: string[];
  constraints: string[];
}

export interface VerifyFallbackView {
  /** verbatim | edited | rejected | unratified | none (no event on the run). */
  outcome: string;
  ratifiedBy: string | null;
  editDistance: number;
  expectation: RatifiedExpectation | null;
  /** green ONLY when the contract was actually ratified (verbatim/edited). */
  tone: "green" | "rose" | "neutral";
  ratified: boolean;
}

/** The run's latest ExpectationRatified event, or null. */
export function findRatifiedEvent(events: RunEvent[]): ExpectationRatifiedEvent | null {
  let latest: ExpectationRatifiedEvent | null = null;
  for (const e of events) {
    if (e.type === EventType.ExpectationRatified) latest = e as ExpectationRatifiedEvent;
  }
  return latest;
}

function pickString(raw: Record<string, unknown>, ...keys: string[]): string {
  for (const k of keys) {
    const v = raw[k];
    if (typeof v === "string") return v;
  }
  return "";
}

function pickStringArray(raw: Record<string, unknown>, ...keys: string[]): string[] {
  for (const k of keys) {
    const v = raw[k];
    if (Array.isArray(v)) return v.filter((x): x is string => typeof x === "string");
  }
  return [];
}

/**
 * Parse a serialized ExpectationDraft (RatifiedJson). The backend serializes
 * with default System.Text.Json (PascalCase); we accept camelCase too so a
 * future options change does not silently blank the panel.
 */
export function parseExpectationJson(json: string): RatifiedExpectation | null {
  try {
    const raw = JSON.parse(json) as Record<string, unknown>;
    return {
      observed: pickString(raw, "Observed", "observed"),
      expected: pickStringArray(raw, "Expected", "expected"),
      constraints: pickStringArray(raw, "Constraints", "constraints"),
    };
  } catch {
    return null;
  }
}

/**
 * Build the fallback Verify view from the run's events. The tone is emerald
 * ONLY for a genuinely ratified contract (verbatim/edited); rejected is rose;
 * an unratified stamp or a missing event is neutral — the UI never shows green
 * the human never ratified.
 */
export function buildVerifyFallback(events: RunEvent[]): VerifyFallbackView {
  const ev = findRatifiedEvent(events);
  if (!ev) {
    return { outcome: "none", ratifiedBy: null, editDistance: 0, expectation: null, tone: "neutral", ratified: false };
  }
  const ratified = ev.outcome === "verbatim" || ev.outcome === "edited";
  const tone: VerifyFallbackView["tone"] = ratified ? "green" : ev.outcome === "rejected" ? "rose" : "neutral";
  return {
    outcome: ev.outcome,
    ratifiedBy: ev.ratifiedBy,
    editDistance: ev.editDistance,
    expectation: parseExpectationJson(ev.ratifiedJson),
    tone,
    ratified,
  };
}
