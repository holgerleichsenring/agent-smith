import {
  EventType,
  type RunEvent,
  type StepStartedEvent,
  type LlmCallStartedEvent,
  type GateCheckedEvent,
  type DecisionLoggedEvent,
  type TriageRouteEvent,
} from "@/types/hub-events";
import type { TrailNode, GateChip } from "@/types/trail-node";

// p0169h: pure-function projection of RunEvent[] → TrailNode tree.
// Level 1 = steps (any non-skill step is a sibling of skill-rounds);
// Level 2 = LLM-call pairs + decisions + triage routes;
// GateChecked attaches as inline chips on the containing step;
// Tool pairs are NOT in the tree (rendered run-level in p0169g for the
// same architectural reason — no repo association on the events).

export interface AssembledTrail {
  root: TrailNode | null;
  truncated: boolean;
}

export function assembleTrail(events: RunEvent[]): AssembledTrail {
  if (events.length === 0) return { root: null, truncated: false };

  const firstEventType = events[0].type;
  const truncated = firstEventType !== EventType.RunStarted;

  const root: TrailNode = {
    id: `run:${events[0].runId}`,
    kind: "run",
    label: deriveRunLabel(events),
    startedAtMs: Date.parse(events[0].timestamp),
    durationMs: deriveRunDurationMs(events),
    payload: null,
    eventTypes: new Set(events.map((e) => e.type)),
    gateChips: [],
    children: [],
  };

  const stepByIndex = new Map<number, TrailNode>();
  const pendingLlmCalls: LlmCallStartedEvent[] = [];

  for (const event of events) {
    switch (event.type) {
      case EventType.StepStarted: {
        const node: TrailNode = {
          id: `step:${event.stepIndex}`,
          kind: "step",
          label: `${event.stepIndex}. ${event.stepName}`,
          startedAtMs: Date.parse(event.timestamp),
          durationMs: null,
          payload: event,
          eventTypes: new Set([EventType.StepStarted]),
          gateChips: [],
          children: [],
        };
        stepByIndex.set(event.stepIndex, node);
        root.children.push(node);
        break;
      }
      case EventType.StepFinished: {
        const node = stepByIndex.get(event.stepIndex);
        if (node) {
          node.durationMs = event.durationMs;
          node.payload = [node.payload as StepStartedEvent, event].filter(Boolean) as RunEvent[];
          node.eventTypes.add(EventType.StepFinished);
        }
        break;
      }
      case EventType.LlmCallStarted: {
        pendingLlmCalls.push(event);
        break;
      }
      case EventType.LlmCallFinished: {
        const finished = event;
        const start = pendingLlmCalls.findIndex((c) => c.model === finished.model && c.role === finished.role);
        if (start === -1) break;
        const started = pendingLlmCalls.splice(start, 1)[0];
        const parent = mostRecentStep(stepByIndex, Date.parse(finished.timestamp));
        if (!parent) break;
        const child: TrailNode = {
          id: `llm:${parent.id}:${started.promptHash}`,
          kind: "skill-call",
          label: `${started.role} → ${started.model}`,
          startedAtMs: Date.parse(started.timestamp),
          durationMs: finished.durationMs,
          payload: [started, finished],
          eventTypes: new Set([EventType.LlmCallStarted, EventType.LlmCallFinished]),
          gateChips: [],
          children: [],
        };
        parent.children.push(child);
        parent.eventTypes.add(EventType.LlmCallStarted);
        parent.eventTypes.add(EventType.LlmCallFinished);
        break;
      }
      case EventType.DecisionLogged: {
        const decision = event as DecisionLoggedEvent;
        const parent = mostRecentStep(stepByIndex, Date.parse(decision.timestamp)) ?? root;
        parent.children.push({
          id: `decision:${parent.id}:${parent.children.length}`,
          kind: "decision",
          label: `${decision.category} — ${decision.chose}`,
          startedAtMs: Date.parse(decision.timestamp),
          durationMs: null,
          payload: decision,
          eventTypes: new Set([EventType.DecisionLogged]),
          gateChips: [],
          children: [],
        });
        parent.eventTypes.add(EventType.DecisionLogged);
        break;
      }
      case EventType.TriageRoute: {
        const route = event as TriageRouteEvent;
        const parent = mostRecentStep(stepByIndex, Date.parse(route.timestamp)) ?? root;
        parent.children.push({
          id: `triage:${parent.id}:${parent.children.length}`,
          kind: "triage",
          label: `${route.role}: ${route.skill}`,
          startedAtMs: Date.parse(route.timestamp),
          durationMs: null,
          payload: route,
          eventTypes: new Set([EventType.TriageRoute]),
          gateChips: [],
          children: [],
        });
        parent.eventTypes.add(EventType.TriageRoute);
        break;
      }
      case EventType.GateChecked: {
        const gate = event as GateCheckedEvent;
        const parent = mostRecentStep(stepByIndex, Date.parse(gate.timestamp)) ?? root;
        const chip: GateChip = { gate: gate.gate, passed: gate.passed, reason: gate.reason };
        parent.gateChips.push(chip);
        parent.eventTypes.add(EventType.GateChecked);
        break;
      }
      default:
        break;
    }
  }

  return { root, truncated };
}

function deriveRunLabel(events: RunEvent[]): string {
  const started = events.find((e) => e.type === EventType.RunStarted);
  if (started && started.type === EventType.RunStarted) {
    return `${started.pipeline} · ${started.runId}`;
  }
  return events[0].runId;
}

function deriveRunDurationMs(events: RunEvent[]): number | null {
  const started = events.find((e) => e.type === EventType.RunStarted);
  const finished = events.find((e) => e.type === EventType.RunFinished);
  if (!started || !finished) return null;
  return Date.parse(finished.timestamp) - Date.parse(started.timestamp);
}

function mostRecentStep(map: Map<number, TrailNode>, atMs: number): TrailNode | null {
  let best: TrailNode | null = null;
  for (const node of map.values()) {
    if (node.startedAtMs <= atMs && (!best || node.startedAtMs >= best.startedAtMs)) best = node;
  }
  return best;
}
