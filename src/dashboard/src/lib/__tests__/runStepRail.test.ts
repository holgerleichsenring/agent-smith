import { describe, it, expect } from "vitest";
import { splitPhasePrefix, toRailNodes } from "../runStepRail";
import type { RunStepRow } from "../runStepsApi";

// p0395: spliced phase steps carry their phase as structured data, never as a
// per-row label prefix. Current servers send RunStepView.phaseId with clean
// names; payloads from older servers still carry the composed "p<id>: <label>"
// names, so the rail splits the known prefix defensively.

function row(over: Partial<RunStepRow>): RunStepRow {
  return {
    stepIndex: 0, stepName: "Fetch ticket", displayName: null, commandName: null,
    status: "success", durationSeconds: 1, resultMessage: null,
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
    ...over,
  };
}

describe("splitPhasePrefix", () => {
  it("splits the known phase-qualified shape into phase and label", () => {
    expect(splitPhasePrefix("p19106a: Generate plan"))
      .toEqual({ phaseId: "p19106a", label: "Generate plan" });
  });

  it("leaves labels without the shape untouched", () => {
    expect(splitPhasePrefix("Fetch ticket")).toEqual({ phaseId: null, label: "Fetch ticket" });
    expect(splitPhasePrefix("prod: deploy")).toEqual({ phaseId: null, label: "prod: deploy" });
  });
});

describe("toRailNodes phase handling", () => {
  it("prefers the server's structured phaseId and shows the clean label", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 0, displayName: "Generate plan", phaseId: "p19106a" }),
    ]);

    expect(nodes[0].label).toBe("Generate plan");
    expect(nodes[0].phaseId).toBe("p19106a");
  });

  it("splits a legacy prefixed label from an old server defensively", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 0, displayName: "p19106a: Generate plan" }),
    ]);

    expect(nodes[0].label).toBe("Generate plan");
    expect(nodes[0].phaseId).toBe("p19106a");
  });

  it("carries no phase on an unspliced step", () => {
    const nodes = toRailNodes([row({ stepIndex: 0, displayName: "Fetch ticket" })]);

    expect(nodes[0].label).toBe("Fetch ticket");
    expect(nodes[0].phaseId).toBeNull();
  });
});
