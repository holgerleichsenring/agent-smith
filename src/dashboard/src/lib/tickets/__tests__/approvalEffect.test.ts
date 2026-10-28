import { describe, expect, it } from "vitest";
import { approvalEffectLabel } from "../approvalEffect";

// 2026-10-13-9a59r: every 9a59l effect and every 9a59m start arm has a label of its own, from one table.

const EFFECTS = ["bug-ticket", "follow-up-closed", "amend-never-worked", "refused-over-cap", "refused", "continue", "comment"];
const ARMS = ["start-now", "wait-for-run", "refused-question-pending", "recorded-without-runs-control", "answers-handback"];

describe("approvalEffectLabel", () => {
  it("EachEffect_RendersItsOwnLabel", () => {
    const labels = EFFECTS.map((effect) => approvalEffectLabel({ effect }).effect);
    expect(new Set(labels).size).toBe(EFFECTS.length);
    for (let i = 0; i < EFFECTS.length; i++) expect(labels[i]).not.toBe(`Approving ${EFFECTS[i]}.`);
  });

  it("EachStartArm_RendersItsOwnLabel_AndOnlyQuestionPendingWithholdsApprove", () => {
    const labels = ARMS.map((kind) => approvalEffectLabel({ effect: "continue", start: { kind } }));
    expect(new Set(labels.map((l) => l.start)).size).toBe(ARMS.length);
    expect(labels.filter((l) => !l.approvable).map((l) => l.start)).toEqual([labels[2].start]);
    expect(approvalEffectLabel({ effect: "continue" }).start).toBeNull();
  });
});
