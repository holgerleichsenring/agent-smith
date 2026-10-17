import { isRunLive } from "@/lib/runLiveness";
import { shortRepo } from "@/lib/shortRepo";
import type {
  RunPass,
  RunPasses,
  TicketAttempt,
  TicketConversationEntry,
  TicketSeries,
  TicketSeriesSpec,
} from "@/types/tickets";
import { handbackCaseWords, money, passEndWords, refusedReviews } from "./flowWords";
import { questionJoin, type QuestionPair } from "./questionJoin";
import { isRunRecord } from "./specWords";

// 2026-10-13-9a59q: one attempt of the code pipeline as a graph — a PURE function of the attempt's own
// records, so its shape is tested without a DOM and the SVG only paints. Columns and row heights are
// ticket-view.html's. Head: TICKET -> DERIVE -> GATE, or -> HAND-BACK. Body: one row per spec of the
// attempt's cut; a carried spec is one small dashed node; a spec that ran or will run is SPEC -> BUILD ->
// VERIFY, one lane per pass, numbered by passInSpec. Tail: SHIP or ENDS.
// 2026-10-16-7ed3d: labels and metrics are cut to their column (the full words stay on the node for the tooltip and
// the detail); a re-engage loop runs outside the pass boxes and writes its reason once.

export type FlowMode = "unrolled" | "folded";
export type FlowState = "done" | "carried" | "live" | "bad" | "idle" | "person";
export type FlowKind = "ticket" | "derive" | "gate" | "handback" | "spec" | "build" | "verify" | "question" | "tail";
export type FlowIcon = "ticket" | "derive" | "gate" | "hand" | "spec" | "build" | "verify" | "ask" | "pr" | "stop";

export interface FlowNode {
  id: string;
  kind: FlowKind;
  x: number;
  y: number;
  r: number;
  state: FlowState;
  /** Cut to the column; fullLabel / fullMetric keep the words. */
  label: string;
  metric: string;
  fullLabel: string;
  fullMetric: string;
  icon: FlowIcon;
  badge?: string;
  specId?: string;
  /** The pass a build, verify or question node stands for. */
  pass?: RunPass;
  passCount?: number;
}

export interface FlowEdge { cls: string; d: string }
export interface FlowText { cls: string; x: number; y: number; text: string; middle?: boolean; title?: string }
export interface FlowLane { x: number; y: number; width: number; height: number }

export interface AttemptFlowLayout {
  /** A terminal attempt with no revision and no cut predates the record: no graph. */
  fallback: boolean;
  nodes: FlowNode[];
  edges: FlowEdge[];
  texts: FlowText[];
  lanes: FlowLane[];
  height: number;
}

export interface AttemptFlowInput {
  attempt: TicketAttempt;
  nextAttempt: TicketAttempt | null;
  /** series?runId= of this attempt. */
  series: TicketSeries | null;
  passes: RunPasses | null;
  /** This attempt's conversation entries, in the order the read serves them. */
  entries: TicketConversationEntry[];
}

export const FLOW_X = { ticket: 70, derive: 190, gate: 310, spec: 440, build: 600, verify: 770, ship: 1020 };
export const FLOW_WIDTH = 1130;
const R = 26, RS = 18, HEAD_Y = 80, ROW = 150, CARRY = 72, ASK_ROOM = 44;
/** Characters a node's label or metric may take before it reaches the next column's. */
export const LABEL_MAX = 24;
/** A pass box: from 50 above its lane to 62 below, 58 beyond build and verify. */
const BOX_TOP = 50, BOX_BOTTOM = 62, BOX_SIDE = 58, LOOP_OUT = 12, LOOP_DROP = 19, REASON_MAX = 42;

interface Builder { nodes: FlowNode[]; edges: FlowEdge[]; texts: FlowText[]; lanes: FlowLane[] }
interface Cursor { x: number; y: number; state: FlowState; small: boolean; specY?: number }

export function layoutAttemptFlow(input: AttemptFlowInput, mode: FlowMode): AttemptFlowLayout {
  const { attempt, series } = input;
  const live = isRunLive(attempt.status);
  // A run-record read is no cut: such an attempt keeps the run-viewer link.
  const cut = series?.runId === attempt.runId && !isRunRecord(series) ? series : null;
  if (!live && !attempt.revision && !cut) return { fallback: true, nodes: [], edges: [], texts: [], lanes: [], height: 0 };

  const b: Builder = { nodes: [], edges: [], texts: [], lanes: [] };
  const y0 = HEAD_Y;
  const handedBack = attempt.handedBack || cut?.handback != null;
  const deriveState: FlowState = cut || handedBack ? "done" : live ? "live" : "bad";
  b.nodes.push(node("ticket", "ticket", FLOW_X.ticket, y0, R, "done", "TICKET", ticketMetric(input), "ticket"));
  b.edges.push({ cls: edgeClass("done", deriveState), d: seg(FLOW_X.ticket, y0, FLOW_X.derive, y0, R, R) });
  const refused = refusedReviews(input.entries);
  b.nodes.push({
    ...node("derive", "derive", FLOW_X.derive, y0, R, deriveState, "DERIVE", deriveMetric(input, cut !== null), "derive"),
    badge: refused > 0 ? `${refused}✗` : undefined,
  });
  if (live && !cut && !handedBack) return finish(b, y0);

  let last: Cursor = { x: FLOW_X.derive, y: y0, state: deriveState, small: false };
  if (handedBack) {
    b.edges.push({ cls: "person", d: seg(FLOW_X.derive, y0, FLOW_X.gate, y0, R, R) });
    const words = cut?.handback ? handbackCaseWords(cut.handback.case) : "";
    b.nodes.push(node("handback", "handback", FLOW_X.gate, y0, R, "person", "HAND-BACK", words, "hand"));
    last = { x: FLOW_X.gate, y: y0, state: "person", small: false };
  } else {
    last = body(b, input, cut, live, mode);
  }
  tail(b, input, live, handedBack, last);
  return finish(b, last.y);
}

function body(b: Builder, input: AttemptFlowInput, cut: TicketSeries | null, live: boolean, mode: FlowMode): Cursor {
  const y0 = HEAD_Y;
  const gate = cut?.gate;
  const gateState: FlowState = gate?.status === "passed" ? "done" : gate?.status === "failed" ? "bad" : live ? "live" : "idle";
  b.edges.push({ cls: edgeClass("done", gateState), d: seg(FLOW_X.derive, y0, FLOW_X.gate, y0, R, R) });
  b.nodes.push(node("gate", "gate", FLOW_X.gate, y0, R, gateState, "GATE", gate ? `${gate.specCount} specs` : "", "gate"));
  const specs = [...(cut?.specs ?? [])].sort((l, r) => l.ordinal - r.ordinal);
  b.texts.push({ cls: "speclab", x: FLOW_X.spec - 22, y: y0 - 46, text: `SERIES · ${specs.length} SPECS` });
  const asks = questionJoin(input.passes, input.entries);
  let last: Cursor = { x: FLOW_X.gate, y: y0, state: gateState, small: false };
  let y = y0;
  specs.forEach((spec, index) => {
    const lanes = passLanes(spec, input.passes, live);
    if (spec.state !== "carried" && lanes[0]?.endedBecause === "question" && index > 0) y += ASK_ROOM;
    const state = specState(spec, live);
    const small = spec.state === "carried";
    const from = index === 0 ? { x: FLOW_X.gate, y: y0, small: false } : { x: FLOW_X.spec, y: last.specY ?? y, small: last.small };
    b.edges.push({ cls: edgeClass(index === 0 ? gateState : last.state, state), d: seg(from.x, from.y, FLOW_X.spec, y, from.small ? RS : R, small ? RS : R) });
    if (small) {
      carriedRow(b, spec, y);
      last = { x: FLOW_X.spec, y, state: "carried", small: true, specY: y };
      y += CARRY;
      return;
    }
    const end = specRow(b, spec, lanes, state, y, mode, asks.pairs);
    last = { ...end, specY: y };
    y += end.height + 6;
  });
  return last;
}

type LaneOrStub = RunPass & { stub?: FlowState };

/** The spec's recorded passes, or for a spec with none one stub lane: working while live, else idle. */
function passLanes(spec: TicketSeriesSpec, passes: RunPasses | null, live: boolean): LaneOrStub[] {
  const recorded = [...(passes?.specs.find((s) => s.specId === spec.specId)?.passes ?? [])]
    .sort((l, r) => l.passInSpec - r.passInSpec);
  if (recorded.length > 0) return recorded;
  const working = spec.state === "in_progress" && live;
  return [stubPass(working ? "live" : "idle")];
}

function stubPass(stub: FlowState): LaneOrStub {
  return {
    ordinal: 0, passInSpec: 1, passInStep: null, openedBecause: null, endedBecause: "", reengageReason: null,
    verdictEmitted: null, criteriaMet: null, criteriaTotal: null, criteria: null, gateLine: null,
    actionablePending: null, unbackedDone: null, endedAt: null, stepsMoved: [], costUsd: 0, calls: 0, stub,
  };
}

function specRow(
  b: Builder, spec: TicketSeriesSpec, lanes: LaneOrStub[], state: FlowState, y: number, mode: FlowMode,
  asks: ReadonlyMap<number, QuestionPair>,
): Cursor & { height: number } {
  const folded = mode === "folded" && lanes.length > 1;
  const shown = folded ? [lanes[lanes.length - 1]] : lanes;
  const count = lanes.length;
  b.nodes.push({ ...node(spec.specId, "spec", FLOW_X.spec, y, R, state, specLabel(spec), specMetric(spec, lanes), "spec"), specId: spec.specId });
  let cursor: Cursor = { x: FLOW_X.spec, y, state, small: false };
  const ys = laneYs(shown, y);
  shown.forEach((pass, i) => {
    const ly = ys[i];
    const number = pass.passInSpec;
    const suffix = `${spec.specId}-${number}`;
    if (count > 1 && !folded) {
      b.lanes.push({ x: FLOW_X.build - BOX_SIDE, y: ly - BOX_TOP, width: FLOW_X.verify - FLOW_X.build + 2 * BOX_SIDE, height: BOX_TOP + BOX_BOTTOM });
      b.texts.push({ cls: "passlab", x: FLOW_X.build - 46, y: ly - 32, text: `PASS ${number} OF ${count}` });
    }
    const [buildState, verifyState] = passStates(pass, spec, lanes);
    if (i === 0) b.edges.push({ cls: edgeClass(state, buildState), d: seg(FLOW_X.spec, y, FLOW_X.build, ly, R, R) });
    b.edges.push({ cls: edgeClass(buildState, verifyState), d: seg(FLOW_X.build, ly, FLOW_X.verify, ly, R, R) });
    const badge = folded ? `×${count}` : undefined;
    const real = pass.stub ? undefined : pass;
    b.nodes.push({ ...node(`b-${suffix}`, "build", FLOW_X.build, ly, R, buildState, "BUILD", buildMetric(pass, spec), "build"), badge, specId: spec.specId, pass: real, passCount: count });
    b.nodes.push({ ...node(`v-${suffix}`, "verify", FLOW_X.verify, ly, R, verifyState, "VERIFY", verifyMetric(pass, spec), "verify"), badge, specId: spec.specId, pass: real, passCount: count });
    if (real && real.endedBecause === "question") askNode(b, spec, real, ly, suffix, asks);
    const next = shown[i + 1];
    if (next && !folded) returnEdge(b, pass, next, ly, ys[i + 1]);
    cursor = { x: FLOW_X.verify, y: ly, state: verifyState, small: false };
  });
  return { ...cursor, height: ys[ys.length - 1] - y + ROW };
}

/** Each lane's y: one ROW apart, and a later lane whose pass parked on a question starts ASK_ROOM lower, so its
 *  question sits below the loop that enters it; every lane after it shifts too. */
function laneYs(shown: LaneOrStub[], y: number): number[] {
  const ys: number[] = [];
  shown.forEach((pass, i) => {
    const room = i > 0 && !pass.stub && pass.endedBecause === "question" ? ASK_ROOM : 0;
    ys.push(i === 0 ? y : ys[i - 1] + ROW + room);
  });
  return ys;
}

function askNode(b: Builder, spec: TicketSeriesSpec, pass: RunPass, ly: number, suffix: string, asks: ReadonlyMap<number, QuestionPair>) {
  const ax = (FLOW_X.build + FLOW_X.verify) / 2, ay = ly - 70;
  const pair = asks.get(pass.ordinal);
  b.edges.push({ cls: "person", d: seg(FLOW_X.build, ly, ax, ay, R, RS) });
  const metric = pair?.parkedMs != null ? `parked ${duration(pair.parkedMs)}` : "";
  b.nodes.push({ ...node(`ask-${suffix}`, "question", ax, ay, RS, "person", "QUESTION", metric, "ask"), specId: spec.specId, pass });
}

/** Right out of the verify node to beside the box, down into the gap under it, left along the gap, down beside the
 *  next box and into its build node — never through a box. One reason, above the run. */
function returnEdge(b: Builder, ended: RunPass, next: RunPass, ly: number, yn: number) {
  const right = FLOW_X.verify + BOX_SIDE + LOOP_OUT, left = FLOW_X.build - BOX_SIDE - LOOP_OUT, run = ly + BOX_BOTTOM + LOOP_DROP;
  b.edges.push({ cls: "back", d: `M${FLOW_X.verify + R} ${ly} H${right} V${run} H${left} V${yn} H${FLOW_X.build - R}` });
  const words = `↺ ${passEndWords(ended.reengageReason ?? next.openedBecause ?? ended.endedBecause)}`;
  b.texts.push({ cls: "elab", x: (FLOW_X.build + FLOW_X.verify) / 2, y: run - 5, text: fit(words, REASON_MAX), middle: true, title: words });
}

function carriedRow(b: Builder, spec: TicketSeriesSpec, y: number) {
  b.nodes.push({ ...node(spec.specId, "spec", FLOW_X.spec, y, RS, "carried", `SPEC ${spec.ordinal}`, "", "spec"), specId: spec.specId });
  b.edges.push({ cls: "carried", d: `M${FLOW_X.spec + RS + 8} ${y} L${FLOW_X.build - 10} ${y}` });
  const where = spec.executedByAttempt != null ? `A${spec.executedByAttempt}` : "an earlier attempt";
  b.texts.push({ cls: "carrytxt", x: FLOW_X.build, y: y + 4, text: `executed in ${where} · carried · $0` });
}

function tail(b: Builder, input: AttemptFlowInput, live: boolean, handedBack: boolean, last: Cursor) {
  const { attempt, nextAttempt } = input;
  const opened = attempt.pullRequests.filter((p) => p.status === "opened").length;
  const succeeded = attempt.status === "success" || attempt.status === "shortfall";
  const ship = !handedBack && (live || succeeded);
  const state: FlowState = handedBack ? "person" : live ? "idle" : succeeded ? "done" : "bad";
  const metric = handedBack ? "handed back" : live ? "not yet" : succeeded ? prWords(opened) : endWords(attempt);
  const ty = last.y;
  if (last.small) b.edges.push({ cls: "carried", d: `M${FLOW_X.spec + RS + 8} ${ty} L${FLOW_X.ship - R} ${ty}` });
  else b.edges.push({ cls: state === "done" || state === "bad" || state === "person" ? state : "idle", d: seg(last.x, ty, FLOW_X.ship, ty, R, R) });
  b.nodes.push(node("tail", "tail", FLOW_X.ship, ty, R, state, ship ? "SHIP" : "ENDS", metric, ship ? "pr" : "stop"));
  if (nextAttempt && !live) {
    b.edges.push({ cls: "next", d: `M${FLOW_X.ship + R + 10} ${ty} l40 0` });
    b.texts.push({ cls: "elab next", x: FLOW_X.ship + R + 14, y: ty - 10, text: `→ attempt ${nextAttempt.number}` });
  }
}

function finish(b: Builder, lastY: number): AttemptFlowLayout {
  return { fallback: false, ...b, height: Math.max(lastY + 90, 200) };
}

function node(id: string, kind: FlowKind, x: number, y: number, r: number, state: FlowState, label: string, metric: string, icon: FlowIcon): FlowNode {
  return { id, kind, x, y, r, state, label: fit(label, LABEL_MAX), metric: fit(metric, LABEL_MAX), fullLabel: label, fullMetric: metric, icon };
}

/** The text cut to max characters, the cut marked with an ellipsis. */
export function fit(text: string, max: number): string {
  return text.length <= max ? text : `${text.slice(0, max - 1).trimEnd()}…`;
}

export function specState(spec: TicketSeriesSpec, live: boolean): FlowState {
  switch (spec.state) {
    case "done": return "done";
    case "carried": return "carried";
    case "failed": return "bad";
    case "handed_back": return "person";
    case "in_progress": return live ? "live" : "bad";
    default: return "idle";
  }
}

function passStates(pass: LaneOrStub, spec: TicketSeriesSpec, lanes: LaneOrStub[]): [FlowState, FlowState] {
  if (pass.stub) return [pass.stub, "idle"];
  const open = pass.endedBecause === "unknown" && pass === lanes[lanes.length - 1] && spec.state === "in_progress";
  if (open) return ["live", "idle"];
  const build: FlowState = pass.endedBecause === "failed" ? "bad" : "done";
  if (pass.criteriaTotal == null || pass.criteriaMet == null || pass.verdictEmitted === false) return [build, "idle"];
  return [build, pass.criteriaMet >= pass.criteriaTotal ? "done" : "bad"];
}

function buildMetric(pass: LaneOrStub, spec: TicketSeriesSpec): string {
  if (pass.stub === "idle") return `${spec.steps.length} steps`;
  if (pass.stub === "live") return "working";
  const done = new Set(pass.stepsMoved.filter((t) => t.to === "done").map((t) => t.entryId)).size;
  return done > 0 ? `${done} steps · ${money(pass.costUsd)}` : money(pass.costUsd);
}

function verifyMetric(pass: LaneOrStub, spec: TicketSeriesSpec): string {
  if (pass.stub === "idle") return `${spec.done.length} criteria`;
  if (pass.stub === "live" || pass.endedBecause === "unknown") return "not yet";
  if (pass.criteriaTotal != null && pass.criteriaMet != null) return `${pass.criteriaMet} of ${pass.criteriaTotal}`;
  return passEndWords(pass.endedBecause);
}

function specLabel(spec: TicketSeriesSpec): string {
  return spec.repos[0] ? `SPEC ${spec.ordinal} · ${shortRepo(spec.repos[0]).toUpperCase()}` : `SPEC ${spec.ordinal}`;
}

function specMetric(spec: TicketSeriesSpec, lanes: LaneOrStub[]): string {
  if (spec.state === "not_started") return "cut, not started";
  const recorded = lanes.filter((p) => !p.stub);
  return recorded.length > 0 ? money(recorded.reduce((sum, p) => sum + p.costUsd, 0)) : "";
}

function ticketMetric(input: AttemptFlowInput): string {
  return input.entries.some((e) => e.source === "ticket" && e.kind === "edited") ? "text changed" : "";
}

function deriveMetric(input: AttemptFlowInput, cut: boolean): string {
  const cost = input.passes && input.passes.derive.calls > 0 ? money(input.passes.derive.costUsd) : "";
  return [cut ? "cut" : "", cost].filter(Boolean).join(" · ");
}

function prWords(opened: number): string {
  return opened === 0 ? "no PR opened" : opened === 1 ? "1 PR" : `${opened} PRs`;
}

function endWords(attempt: TicketAttempt): string {
  return attempt.endedOnInfrastructure ? "ended on infrastructure" : attempt.status;
}

export function duration(ms: number): string {
  const minutes = Math.max(0, Math.round(ms / 60000));
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  return minutes % 60 === 0 ? `${hours}h` : `${hours}h ${minutes % 60}m`;
}

function seg(ax: number, ay: number, bx: number, by: number, ra: number, rb: number): string {
  const dx = bx - ax, dy = by - ay, length = Math.sqrt(dx * dx + dy * dy) || 1, ux = dx / length, uy = dy / length;
  return `M${(ax + ux * ra).toFixed(1)} ${(ay + uy * ra).toFixed(1)} L${(bx - ux * rb).toFixed(1)} ${(by - uy * rb).toFixed(1)}`;
}

function edgeClass(a: FlowState, b: FlowState): string {
  if (a === "live" || b === "live") return "live";
  if (a === "bad" || b === "bad") return "bad";
  if (a === "person" || b === "person") return "person";
  if (a === "idle" || b === "idle") return "idle";
  if (a === "carried" && b === "carried") return "carried";
  return "done";
}
