import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TrailNodeDetailPane } from "../TrailNodeDetailPane";
import {
  EventType,
  type DecisionLoggedEvent,
  type LlmCallFinishedEvent,
  type LlmCallStartedEvent,
} from "@/types/hub-events";
import type { TrailNode } from "@/types/trail-node";

function llmPairNode(): TrailNode {
  const started: LlmCallStartedEvent = {
    type: EventType.LlmCallStarted,
    runId: "r",
    timestamp: new Date().toISOString(),
    model: "claude-opus-4-7",
    role: "Lead",
    promptHash: "deadbeef",
  };
  const finished: LlmCallFinishedEvent = {
    type: EventType.LlmCallFinished,
    runId: "r",
    timestamp: new Date().toISOString(),
    model: "claude-opus-4-7",
    role: "Lead",
    tokensIn: 123,
    tokensOut: 45,
    costUsd: 0.0012,
    durationMs: 800,
  };
  return {
    id: "llm:step:1:deadbeef",
    kind: "skill-call",
    label: "Lead → claude-opus-4-7",
    startedAtMs: Date.parse(started.timestamp),
    durationMs: 800,
    payload: [started, finished],
    eventTypes: new Set([EventType.LlmCallStarted, EventType.LlmCallFinished]),
    gateChips: [],
    children: [],
  };
}

function decisionNode(): TrailNode {
  const event: DecisionLoggedEvent = {
    type: EventType.DecisionLogged,
    runId: "r",
    timestamp: new Date().toISOString(),
    category: "Architecture",
    chose: "Pick X",
    over: "Pick Y",
    reason: "Because foo",
  };
  return {
    id: "decision:1",
    kind: "decision",
    label: "Architecture — Pick X",
    startedAtMs: Date.parse(event.timestamp),
    durationMs: null,
    payload: event,
    eventTypes: new Set([EventType.DecisionLogged]),
    gateChips: [],
    children: [],
  };
}

describe("TrailNodeDetailPane", () => {
  it("renders empty-state when no node is selected", () => {
    render(<TrailNodeDetailPane node={null} />);
    expect(screen.getByTestId("trail-detail-empty")).toBeInTheDocument();
  });

  it("renders LLM call payload with token + cost + hash", () => {
    render(<TrailNodeDetailPane node={llmPairNode()} />);
    expect(screen.getByTestId("llm-call-payload")).toBeInTheDocument();
    expect(screen.getByText("123")).toBeInTheDocument();
    expect(screen.getByText("45")).toBeInTheDocument();
    expect(screen.getByText("deadbeef")).toBeInTheDocument();
  });

  it("renders decision payload with chose + over + reason", () => {
    render(<TrailNodeDetailPane node={decisionNode()} />);
    const pane = screen.getByTestId("decision-payload");
    expect(pane).toBeInTheDocument();
    expect(pane.textContent).toContain("Pick X");
    expect(pane.textContent).toContain("Pick Y");
    expect(pane.textContent).toContain("Because foo");
  });
});
