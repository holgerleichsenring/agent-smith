import { describe, it, expect } from "vitest";
import { acceptSpecNames } from "../acceptedNames";

// 2026-10-12-967aa: a body in the spec vocabulary reads exactly as the phase one this bundle
// renders, and a body in the phase vocabulary is untouched.
describe("acceptSpecNames", () => {
  it("reads a spec-named step event as the phase-named one", () => {
    const event = acceptSpecNames<unknown>({ type: 4, runId: "r1", specId: "s1" }) as Record<string, unknown>;
    expect(event.phaseId).toBe("s1");
  });

  it("reads a spec-named proposal push, nested, with its kind", () => {
    const push = acceptSpecNames<unknown>({
      kind: "spec",
      spec: { specId: "2026-10-12-967aa", goal: "g", findings: [{ specId: "x" }] },
    }) as { kind: string; phase: { phaseId: string; findings: Array<{ phaseId: string }> } };
    expect(push.kind).toBe("phase");
    expect(push.phase.phaseId).toBe("2026-10-12-967aa");
    expect(push.phase.findings[0].phaseId).toBe("x");
  });

  it("reads specs and executedSpecIds as phases and executedPhaseIds", () => {
    const set = acceptSpecNames<unknown>({ specs: [{ specId: "a" }], executedSpecIds: ["a"] }) as {
      phases: Array<{ phaseId: string }>; executedPhaseIds: string[];
    };
    expect(set.phases[0].phaseId).toBe("a");
    expect(set.executedPhaseIds).toEqual(["a"]);
  });

  it("leaves a phase-named body and every current name as they were", () => {
    const body = { phaseId: "p", specId: "other", kind: "bug", phase: "Primary" };
    expect(acceptSpecNames({ ...body })).toEqual(body);
  });

  it("passes primitives and null through", () => {
    expect(acceptSpecNames(null)).toBeNull();
    expect(acceptSpecNames("spec")).toBe("spec");
  });
});
