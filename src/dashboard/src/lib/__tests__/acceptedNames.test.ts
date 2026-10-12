import { describe, it, expect } from "vitest";
import { acceptSpecNames } from "../acceptedNames";

// 2026-10-12-967aa/967ab: a body in the phase vocabulary — served by a replica still on 967aa, or
// read back from a stored trail — reads exactly as the spec one this bundle renders, and a body in
// the spec vocabulary is untouched.
describe("acceptSpecNames", () => {
  it("reads an old-named step event as the spec-named one", () => {
    const event = acceptSpecNames<unknown>({ type: 4, runId: "r1", phaseId: "s1" }) as Record<string, unknown>;
    expect(event.specId).toBe("s1");
  });

  it("reads an old-named proposal push, nested, with its kind", () => {
    const push = acceptSpecNames<unknown>({
      kind: "phase",
      phase: { phaseId: "2026-10-12-967ab", goal: "g", findings: [{ phaseId: "x" }] },
    }) as { kind: string; spec: { specId: string; findings: Array<{ specId: string }> } };
    expect(push.kind).toBe("spec");
    expect(push.spec.specId).toBe("2026-10-12-967ab");
    expect(push.spec.findings[0].specId).toBe("x");
  });

  it("reads the old set members as specs and executedSpecIds", () => {
    const set = acceptSpecNames<unknown>({ phases: [{ phaseId: "a" }], executedPhaseIds: ["a"] }) as {
      specs: Array<{ specId: string }>; executedSpecIds: string[];
    };
    expect(set.specs[0].specId).toBe("a");
    expect(set.executedSpecIds).toEqual(["a"]);
  });

  it("leaves a spec-named body as it was", () => {
    const body = { specId: "s", specs: [], kind: "bug", spec: null };
    expect(acceptSpecNames({ ...body })).toEqual(body);
  });

  it("passes primitives and null through", () => {
    expect(acceptSpecNames(null)).toBeNull();
    expect(acceptSpecNames("phase")).toBe("phase");
  });
});
