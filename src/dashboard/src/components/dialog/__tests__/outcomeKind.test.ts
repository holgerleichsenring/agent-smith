import { describe, expect, it } from "vitest";
import { outcomeKindWord } from "../outcomeKind";

describe("outcomeKindWord", () => {
  it("OutcomeKindWord_TheServedPhaseKind_ReadsAsSpec", () => {
    expect(outcomeKindWord("phase")).toBe("spec");
  });

  it("OutcomeKindWord_EveryOtherKind_IsShownAsServed", () => {
    expect(["bug", "epic", "spec"].map(outcomeKindWord)).toEqual(["bug", "epic", "spec"]);
  });
});
