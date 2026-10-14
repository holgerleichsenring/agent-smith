import { describe, expect, it } from "vitest";
import { handbackCaseWords } from "../flowWords";

// 2026-10-13-8762l: a set handed back over a standing review objection is published with its own case.

describe("flowWords", () => {
  it("handbackCaseWords_ReviewObjection_IsReviewObjection", () => {
    expect(handbackCaseWords("ReviewObjection")).toBe("review objection");
    expect(handbackCaseWords("Question")).toBe("question");
  });
});
