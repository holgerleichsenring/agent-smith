"use client";

import {
  EventType,
  type DecisionLoggedEvent,
  type LlmCallFinishedEvent,
  type LlmCallStartedEvent,
  type RunEvent,
  type TriageRouteEvent,
} from "@/types/hub-events";
import type { TrailNode } from "@/types/trail-node";
import { LlmCallPayload } from "./payload/LlmCallPayload";
import { DecisionPayload } from "./payload/DecisionPayload";
import { StepPayload } from "./payload/StepPayload";

interface Props {
  node: TrailNode | null;
}

export function TrailNodeDetailPane({ node }: Props) {
  if (!node) {
    return (
      <div
        className="flex h-full items-center justify-center rounded-lg border border-stone-200 bg-white p-6 text-sm text-stone-500"
        data-testid="trail-detail-empty"
      >
        Select a node to see its payload.
      </div>
    );
  }

  return (
    <div className="space-y-4 rounded-lg border border-stone-200 bg-white p-6" data-testid="trail-detail">
      <header>
        <p className="text-xs uppercase tracking-wide text-stone-400">{node.kind}</p>
        <h2 className="text-lg font-medium text-stone-900">{node.label}</h2>
      </header>
      {renderPayload(node)}
    </div>
  );
}

function renderPayload(node: TrailNode) {
  const payload = node.payload;
  if (payload === null) {
    return <p className="text-sm text-stone-500">No payload — overview node.</p>;
  }
  if (Array.isArray(payload)) {
    const started = payload.find((e) => e.type === EventType.LlmCallStarted);
    const finished = payload.find((e) => e.type === EventType.LlmCallFinished);
    if (started && finished) {
      return <LlmCallPayload started={started as LlmCallStartedEvent} finished={finished as LlmCallFinishedEvent} />;
    }
    const hasStep = payload.some((e) => e.type === EventType.StepStarted || e.type === EventType.StepFinished);
    if (hasStep) {
      return <StepPayload events={payload as RunEvent[]} />;
    }
    return <PayloadJson value={payload} />;
  }
  if (payload.type === EventType.DecisionLogged) {
    return <DecisionPayload event={payload as DecisionLoggedEvent} />;
  }
  if (payload.type === EventType.TriageRoute) {
    const route = payload as TriageRouteEvent;
    return (
      <div className="space-y-1 text-sm">
        <p className="text-stone-800">
          <span className="text-stone-500">skill:</span> {route.skill}
        </p>
        <p className="text-stone-800">
          <span className="text-stone-500">role:</span> {route.role}
        </p>
        <p className="text-stone-800">
          <span className="text-stone-500">confidence:</span> {route.confidence}
        </p>
      </div>
    );
  }
  if (payload.type === EventType.StepStarted || payload.type === EventType.StepFinished) {
    return <StepPayload events={[payload as RunEvent]} />;
  }
  return <PayloadJson value={payload} />;
}

function PayloadJson({ value }: { value: unknown }) {
  return (
    <pre className="overflow-auto rounded bg-stone-50 p-3 text-xs text-stone-800">
      {JSON.stringify(value, null, 2)}
    </pre>
  );
}
