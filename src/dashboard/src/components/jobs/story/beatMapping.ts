import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import {
  EventType,
  type ExpectationRatifiedEvent,
  type RunEvent,
} from "@/types/hub-events";

// p0344: a run is a STORY, not a pipeline. The ~22 framework steps
// (Load catalog, Publish pipeline name, Analyze codebase, …) fold into five
// beats — Ticket → Plan → Building → Verify → Outcome — the story reads
// left→right. This module is the PURE mapping: it takes the ordered execution
// step nodes (already assembled by useRunExecutionTree) and buckets them into
// the five beats, plus derives the Verify beat's acceptance view from the
// p0328 ExpectationRatified event. No React, fully unit-tested.

export type BeatKey = "ticket" | "plan" | "building" | "verify" | "outcome";
export type BeatStatus = "done" | "active" | "idle" | "fail";

export const BEAT_ORDER: BeatKey[] = ["ticket", "plan", "building", "verify", "outcome"];

export const BEAT_LABELS: Record<BeatKey, string> = {
  ticket: "Ticket",
  plan: "Plan",
  building: "Building",
  verify: "Verify",
  outcome: "Outcome",
};

export interface Beat {
  key: BeatKey;
  label: string;
  status: BeatStatus;
  /** Ordinal (1-based) shown in the marker. */
  index: number;
  /** Execution-node ids folded into this beat — the trace behind the beat. */
  stepIds: string[];
  /** Representative node id to select/scroll to on click; null when the beat
   *  folded no steps (thin data — honest, not fabricated). */
  anchorId: string | null;
}

// Keyword rules that map a step's operator-facing display label (from
// CommandDisplayNames) to the beat it SERVES. Checked in beat order; the first
// hit wins. Deliberately not exhaustive — every pipeline has its own steps and
// framework setup steps (Load catalog, Publish pipeline name, Prepare
// environment, …) intentionally match nothing so they fold into the current
// beat by carry-forward rather than becoming top-level noise.
// Order matters: the first rule that matches wins. Building's specific
// "execute plan" is checked before Plan's generic \bplan\b so "Execute plan"
// reads as build work, not planning.
const KEYWORD_RULES: Array<{ beat: BeatKey; test: RegExp }> = [
  { beat: "ticket", test: /\bticket\b|scope repositor|check ?out source|resolve source|acquire source/i },
  { beat: "building", test: /execute plan|master skill|skill round|generate tests|generate docs|persist|produce bootstrap|filter round|review phase|nuclei|spectral|\bzap\b|\bscan\b|audit|final phase|authenticate/i },
  { beat: "plan", test: /analy[sz]e|generate plan|\bplan\b|negotiate expectation|triage|await approval|non-empty|phase spec/i },
  { beat: "verify", test: /verify|gate|convergence|open questions|findings|merge master|pr diff/i },
  { beat: "outcome", test: /pull request|cross-link|run result|phase record|deliver|post pr|snapshot|write tickets|commit init/i },
];

/** Classify a single step label into a beat, or null when no rule matches. */
export function classifyBeat(label: string): BeatKey | null {
  for (const rule of KEYWORD_RULES) if (rule.test.test(label)) return rule.beat;
  return null;
}

/**
 * Derive a beat's status from the statuses of the step nodes folded into it.
 * fail wins outright; any live/parked step makes the beat active; a partially
 * complete beat (some ok, some not) is active; all-ok is done; nothing started
 * (or no steps) is idle. Never claims done on thin data.
 */
export function deriveBeatStatus(statuses: NodeStatus[]): BeatStatus {
  if (statuses.length === 0) return "idle";
  if (statuses.some((s) => s === "fail")) return "fail";
  if (statuses.some((s) => s === "run" || s === "input" || s === "queued")) return "active";
  const okCount = statuses.filter((s) => s === "ok").length;
  if (okCount === statuses.length) return "done";
  if (okCount > 0) return "active";
  return "idle";
}

/**
 * Fold the ordered execution step nodes into the five beats. A step inherits
 * the current beat when no keyword matches (carry-forward), and the beat cursor
 * is monotonic — a stray late match to an earlier keyword never drags the story
 * backwards — so the beats always read left→right.
 */
export function mapStepsToBeats(nodes: ExecutionNodeProps[]): Beat[] {
  const idsByBeat: Record<BeatKey, string[]> = {
    ticket: [], plan: [], building: [], verify: [], outcome: [],
  };
  const statusByBeat: Record<BeatKey, NodeStatus[]> = {
    ticket: [], plan: [], building: [], verify: [], outcome: [],
  };

  let cursor = 0;
  for (const node of nodes) {
    const matched = classifyBeat(node.label);
    const matchedIdx = matched ? BEAT_ORDER.indexOf(matched) : -1;
    const idx = matchedIdx < 0 ? cursor : Math.max(cursor, matchedIdx);
    cursor = idx;
    const beat = BEAT_ORDER[idx];
    idsByBeat[beat].push(node.id);
    statusByBeat[beat].push(node.status);
  }

  return BEAT_ORDER.map((key, i) => {
    const ids = idsByBeat[key];
    return {
      key,
      label: BEAT_LABELS[key],
      index: i + 1,
      status: deriveBeatStatus(statusByBeat[key]),
      stepIds: ids,
      anchorId: ids[0] ?? null,
    };
  });
}

// --- Verify beat: the ratified acceptance contract (p0328/p0340) ------------

export interface RatifiedExpectation {
  observed: string;
  expected: string[];
  constraints: string[];
}

export interface VerifyView {
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
 * Build the Verify beat view from the run's events. The tone is emerald ONLY
 * for a genuinely ratified contract (verbatim/edited); rejected is rose; an
 * unratified stamp or a missing event is neutral — the UI never shows green the
 * human never ratified.
 *
 * TODO(p0344 follow-up): wire real ProgressLedger once exposed on RunSnapshot —
 * the p0340 per-criterion keystone dispositions and the p0341 ledger coverage
 * warnings (done-step whose target is absent from the diff) are not on the
 * client snapshot yet, so criteria render as "the ratified contract", not
 * per-criterion "proven vs the diff".
 */
export function buildVerifyView(events: RunEvent[]): VerifyView {
  const ev = findRatifiedEvent(events);
  if (!ev) {
    return { outcome: "none", ratifiedBy: null, editDistance: 0, expectation: null, tone: "neutral", ratified: false };
  }
  const ratified = ev.outcome === "verbatim" || ev.outcome === "edited";
  const tone: VerifyView["tone"] = ratified ? "green" : ev.outcome === "rejected" ? "rose" : "neutral";
  return {
    outcome: ev.outcome,
    ratifiedBy: ev.ratifiedBy,
    editDistance: ev.editDistance,
    expectation: parseExpectationJson(ev.ratifiedJson),
    tone,
    ratified,
  };
}
