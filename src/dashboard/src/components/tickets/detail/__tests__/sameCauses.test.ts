import { describe, expect, it } from "vitest";
import { sameCauses } from "../sameCauses";
import { attempt } from "./detailFixtures";

// 2026-10-17-e730e: which failed attempts continue a run of equal causes, from recorded fields only.

describe("sameCauses", () => {
  it("sameCauses_SameKindAndSummaryAsThePrevious_IsSameAsTheFirstOfTheRun", () => {
    const cause = { status: "failed", summary: "Preflight failed: context-image-trust", endKind: "image-untrusted" };
    const attempts = [
      attempt({ ...cause }),
      attempt({ number: 2, runId: "r-2", ...cause }),
      attempt({ number: 3, runId: "r-3", ...cause, status: "error" }),
      attempt({ number: 4, runId: "r-4", status: "failed", summary: "Pipeline aborted: missing bootstrap in repos", endKind: "bootstrap-missing" }),
      attempt({ number: 5, runId: "r-5", status: "failed", summary: "Pipeline aborted: missing bootstrap in repos", endKind: "bootstrap-missing" }),
    ];
    expect([...sameCauses(attempts).entries()]).toEqual([
      ["r-2", { number: 1, key: "r-1" }],
      ["r-3", { number: 1, key: "r-1" }],
      ["r-5", { number: 4, key: "r-4" }],
    ]);
  });

  it("sameCauses_DifferentSummaryOrKind_OrNotFailed_IsNotSameAs", () => {
    const one = attempt({ status: "failed", summary: "Preflight failed: a", endKind: "preflight" });
    const unclassified = sameCauses([
      attempt({ status: "failed", summary: "Pod did not start" }),
      attempt({ number: 2, runId: "r-2", status: "failed", summary: "Pod did not start", endKind: null }),
    ]);
    expect(unclassified.get("r-2")).toEqual({ number: 1, key: "r-1" });
    for (const second of [
      { status: "failed", summary: "Preflight failed: a ", endKind: "preflight" },
      { status: "failed", summary: "Preflight failed: a", endKind: "image-untrusted" },
      { status: "failed", summary: "Preflight failed: a" },
      { status: "cancelled", summary: "Preflight failed: a", endKind: "preflight" },
      { status: "failed", summary: null, endKind: "preflight" },
    ]) {
      expect(sameCauses([one, attempt({ number: 2, runId: "r-2", ...second })]).size).toBe(0);
    }
    expect(sameCauses([attempt({ status: "failed", summary: null }), attempt({ number: 2, runId: "r-2", status: "failed", summary: null })]).size).toBe(0);
  });
});
