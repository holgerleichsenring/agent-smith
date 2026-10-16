import { describe, expect, it } from "vitest";
import { outcomePill } from "../AttemptCard";
import { attempt } from "./detailFixtures";

// 2026-10-16-86e6b: a park closed because a newer attempt started past it reads superseded, before any other mark.

describe("outcomePill", () => {
  it("outcomePill_EndKindSuperseded_ReadsSuperseded", () => {
    expect(outcomePill(attempt({ status: "cancelled", endKind: "superseded", handedBack: true }))).toEqual(["q", "superseded"]);
  });

  it("outcomePill_NoEndKind_KeepsTheStatusPill", () => {
    expect(outcomePill(attempt({ status: "failed" }))).toEqual(["bad", "failed"]);
  });
});
