import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { RunSpecDecision } from "@/lib/runSpecsApi";
import type { StepPass } from "../specPasses";
import { revisionWords, specRevisions } from "../specRevisions";

// 2026-11-26-5201g: the passes that revised a spec, read off its revise_spec decisions and the passes by step index.

const en = translator("en");

function decision(over: Partial<RunSpecDecision>): RunSpecDecision {
  return { stepIndex: null, name: "Use the outbox", reason: "agent", category: "Implementation", recordedAt: "2026-11-26T10:00:00Z", ...over };
}

const pass = (number: number): StepPass => ({ number, opened: null, because: null, ended: null });

describe("specRevisions", () => {
  it("specRevisions_ReviseSpecDecisions_AreNamedByThePassOfTheirStep", () => {
    const passes = new Map([[10, pass(1)], [20, pass(2)], [30, pass(3)]]);
    const revisions = specRevisions([
      decision({ stepIndex: 10, name: "Use the outbox" }),
      decision({ stepIndex: 30, name: "Spec s1 revised in run r1 as revision 2: the operator narrowed it ", reason: "revise-spec" }),
    ], passes);

    expect(revisions).toEqual([{ pass: 3, text: "Spec s1 revised in run r1 as revision 2: the operator narrowed it" }]);
    expect(revisionWords(en, revisions[0])).toBe("revised in pass 3");
  });

  it("specRevisions_OnePassOrNoStep_SaysRevisedWithoutAPass", () => {
    const revisions = specRevisions([
      decision({ stepIndex: 12, name: "Spec s1 revised in run r1 as revision 2: why", reason: "revise-spec" }),
      decision({ stepIndex: null, name: "Spec s1 revised in run r1 as revision 3: why", reason: "revise-spec" }),
    ], new Map());

    expect(revisions.map((r) => r.pass)).toEqual([null, null]);
    expect(revisionWords(en, revisions[0])).toBe("revised");
  });
});
