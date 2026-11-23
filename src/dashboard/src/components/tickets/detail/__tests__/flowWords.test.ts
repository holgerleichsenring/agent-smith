import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { handbackCaseWords, passEndWords } from "../flowWords";

const en = translator("en");

// 2026-10-13-8762l: a set handed back over a standing review objection is published with its own case.

describe("flowWords", () => {
  it("handbackCaseWords_ReviewObjection_IsReviewObjection", () => {
    expect(handbackCaseWords(en, "ReviewObjection")).toBe("review objection");
    expect(handbackCaseWords(en, "Question")).toBe("question");
  });

  // 2026-10-17-a196c: the loop re-engages a master whose met claims cite nothing that ran.
  it("passEndWords_UnresolvedClaims_IsNamed", () => {
    expect(passEndWords(en, "unresolved-claims")).toBe("met claims cite nothing that changed or ran");
  });

  // 2026-11-22-96c8a: open checklist steps are re-driven once, then the loop stops on them.
  it("passEndWords_StepsOpen_AndUnfinished_AreNamed", () => {
    expect(passEndWords(en, "steps-open")).toBe("ended with checklist steps still open");
    expect(passEndWords(en, "unfinished")).toBe("driven once more");
  });
});
