import { describe, it, expect } from "vitest";
import { isRule, patternMatches, refMatches } from "../repoRefs";

// p0488: the one home of the glob rule — the discovered inventory and the repo
// picker both read it, so they can never disagree about what a rule covers.

describe("repoRefs", () => {
  it("PatternMatches_ExactAndGlob_AnchorsWholeName", () => {
    expect(patternMatches("Sample.Api", "Sample.Api")).toBe(true);
    expect(patternMatches("Sample.Api", "Sample.Api.Tests")).toBe(false);
    expect(patternMatches("*", "Anything")).toBe(true);
    expect(patternMatches("Sample.*", "Sample.Web")).toBe(true);
    expect(patternMatches("Sample.*", "Other.Web")).toBe(false);
    expect(patternMatches("*.Api", "Sample.Api")).toBe(true);
    // The dot is a literal, not a regex wildcard.
    expect(patternMatches("Sample.Api", "SampleXApi")).toBe(false);
  });

  it("RefMatches_ScopedToItsConnection_PlainRefsNever", () => {
    expect(refMatches("conn/Sample.Api", "conn", "Sample.Api")).toBe(true);
    expect(refMatches("conn/Sample.*", "conn", "Sample.Web")).toBe(true);
    expect(refMatches("other/Sample.Api", "conn", "Sample.Api")).toBe(false);
    expect(refMatches("legacy", "conn", "legacy")).toBe(false);
    expect(refMatches("/Sample.Api", "", "Sample.Api")).toBe(false);
  });

  it("IsRule_WildcardRefsOnly", () => {
    expect(isRule("conn/*")).toBe(true);
    expect(isRule("conn/Sample.*")).toBe(true);
    expect(isRule("conn/Sample.Api")).toBe(false);
    expect(isRule("legacy")).toBe(false);
  });
});
