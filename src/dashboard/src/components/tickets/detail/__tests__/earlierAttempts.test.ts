import { describe, expect, it } from "vitest";
import { attemptWords, earlierAttempts } from "../earlierAttempts";
import { attempt } from "./detailFixtures";

// 2026-10-16-86e6d: earlier attempts as one line, each row without repeating its pill.

const failed = (number: number, endKind: string | null = null) =>
  attempt({ number, runId: `r-${number}`, status: "failed", endKind });

describe("earlierAttempts", () => {
  it("earlierAttempts_AllStoppedBeforeWork_SaysSo", () => {
    const view = earlierAttempts([failed(1, "image-untrusted"), failed(2, "bootstrap-missing"), failed(3)]);
    expect(view?.line).toBe("2 earlier attempts, all stopped before any work");
    expect(view?.rows.map((r) => r.attempt.number)).toEqual([2, 1]);
    expect(view?.rows[0].words).toBe("failed · bootstrap missing");
  });

  it("earlierAttempts_SupersededAttempt_IsNamed", () => {
    const superseded = attempt({ number: 2, runId: "r-2", status: "cancelled", endKind: "superseded" });
    const worked = { ...failed(1), executedSpecs: [{ specId: "s1", ordinal: 1, title: "t", status: "failed", verdict: null }] };
    const view = earlierAttempts([worked, superseded, failed(3)]);
    expect(view?.line).toBe("2 earlier attempts, A2 superseded");
    expect(attemptWords(superseded)).toBe("superseded");
  });

  it("earlierAttempts_OnlyOneAttempt_IsNull", () => {
    expect(earlierAttempts([failed(1)])).toBeNull();
  });
});
