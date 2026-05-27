import { describe, it, expect } from "vitest";
import { assembleTrail } from "../TrailAssembler";
import {
  EventType,
  type RunEvent,
  type RunStartedEvent,
  type StepStartedEvent,
  type StepFinishedEvent,
  type LlmCallStartedEvent,
  type LlmCallFinishedEvent,
  type GateCheckedEvent,
  type DecisionLoggedEvent,
} from "@/types/hub-events";

const runId = "2026-05-27T13-00-00-bbbb";
const base = Date.parse("2026-05-27T13:00:00.000Z");

function at(deltaSeconds: number): string {
  return new Date(base + deltaSeconds * 1000).toISOString();
}

const runStarted: RunStartedEvent = {
  type: EventType.RunStarted, runId, timestamp: at(0),
  trigger: "ticket", pipeline: "fix-bug", repos: ["server"], startedAt: at(0),
};
const stepStarted = (idx: number, name: string, deltaS: number): StepStartedEvent => ({
  type: EventType.StepStarted, runId, timestamp: at(deltaS),
  stepIndex: idx, stepName: name, totalSteps: 5,
});
const stepFinished = (idx: number, deltaS: number, status = "success"): StepFinishedEvent => ({
  type: EventType.StepFinished, runId, timestamp: at(deltaS),
  stepIndex: idx, status, durationMs: 500,
});
const llmStart = (deltaS: number, model = "claude-opus-4-7"): LlmCallStartedEvent => ({
  type: EventType.LlmCallStarted, runId, timestamp: at(deltaS),
  model, role: "Lead", promptHash: "deadbeef",
});
const llmFinish = (deltaS: number, model = "claude-opus-4-7"): LlmCallFinishedEvent => ({
  type: EventType.LlmCallFinished, runId, timestamp: at(deltaS),
  model, role: "Lead", tokensIn: 100, tokensOut: 50, costUsd: 0.01, durationMs: 1500,
});
const gate: GateCheckedEvent = {
  type: EventType.GateChecked, runId, timestamp: at(2),
  gate: "bootstrap", passed: true, reason: "files present",
};
const decision: DecisionLoggedEvent = {
  type: EventType.DecisionLogged, runId, timestamp: at(4),
  category: "Architecture", chose: "X", over: null, reason: "because",
};

describe("TrailAssembler", () => {
  it("returns no root for an empty event list", () => {
    const { root, truncated } = assembleTrail([]);
    expect(root).toBeNull();
    expect(truncated).toBe(false);
  });

  it("builds steps as children of the run node", () => {
    const events: RunEvent[] = [
      runStarted,
      stepStarted(1, "CheckoutSource", 1),
      stepFinished(1, 2),
      stepStarted(2, "AnalyzeCode", 3),
    ];
    const { root } = assembleTrail(events);
    expect(root).not.toBeNull();
    expect(root!.children).toHaveLength(2);
    expect(root!.children[0].label).toContain("CheckoutSource");
    expect(root!.children[0].durationMs).toBe(500);
    expect(root!.children[1].durationMs).toBeNull();
  });

  it("groups LLM call pairs under the most recent step", () => {
    const events: RunEvent[] = [
      runStarted,
      stepStarted(1, "Triage", 1),
      llmStart(2),
      llmFinish(3),
      stepFinished(1, 4),
    ];
    const { root } = assembleTrail(events);
    const step = root!.children[0];
    expect(step.children).toHaveLength(1);
    expect(step.children[0].kind).toBe("skill-call");
    expect(step.children[0].durationMs).toBe(1500);
  });

  it("attaches GateChecked as inline chips on the parent step, not as a child", () => {
    const events: RunEvent[] = [
      runStarted,
      stepStarted(1, "BootstrapGate", 1),
      gate,
      stepFinished(1, 3),
    ];
    const { root } = assembleTrail(events);
    const step = root!.children[0];
    expect(step.children).toHaveLength(0);
    expect(step.gateChips).toHaveLength(1);
    expect(step.gateChips[0].gate).toBe("bootstrap");
    expect(step.gateChips[0].passed).toBe(true);
  });

  it("renders DecisionLogged as a child node under the most recent step", () => {
    const events: RunEvent[] = [runStarted, stepStarted(1, "Plan", 1), decision];
    const { root } = assembleTrail(events);
    expect(root!.children[0].children).toHaveLength(1);
    expect(root!.children[0].children[0].kind).toBe("decision");
  });

  it("flags truncation when first event is not RunStarted", () => {
    const { truncated, root } = assembleTrail([stepStarted(7, "MidRun", 0)]);
    expect(truncated).toBe(true);
    expect(root).not.toBeNull();
  });

  it("renders parallel skill-rounds with same step index as one step row", () => {
    // The current projector keys steps by stepIndex; two StepStarted with the
    // same index would collide. The runtime emits one StepStarted per batch
    // (PipelineStepRunner), so this is the expected shape.
    const events: RunEvent[] = [
      runStarted,
      stepStarted(1, "SkillRound batch×3", 1),
      llmStart(2),
      llmFinish(3),
      llmStart(2.1),
      llmFinish(3.1),
      stepFinished(1, 4),
    ];
    const { root } = assembleTrail(events);
    expect(root!.children).toHaveLength(1);
    expect(root!.children[0].children).toHaveLength(2);
  });
});
