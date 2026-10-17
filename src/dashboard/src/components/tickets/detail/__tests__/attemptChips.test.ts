import { describe, expect, it } from "vitest";
import { shortRepo } from "@/lib/shortRepo";
import { attemptChips, attemptWords } from "../attemptChips";
import { attempt } from "./detailFixtures";

// 2026-10-16-7ed3b: every attempt as a chip, oldest first, the newest marked, its cause as the title.

const failed = (number: number, endKind: string | null = null, summary: string | null = null) =>
  attempt({ number, runId: `r-${number}`, status: "failed", endKind, summary });

describe("attemptChips", () => {
  it("attemptChips_EveryAttempt_OldestFirstHeadMarkedWithToneAndCauseTitle", () => {
    const superseded = attempt({ number: 2, runId: "r-2", status: "cancelled", endKind: "superseded" });
    const raw = '{"type":"error","error":{"message":"overloaded"},"request_id":"x"}';
    const chips = attemptChips([failed(1, "bootstrap-missing", "\n  Pipeline aborted: missing bootstrap\nmore"), superseded, failed(3, null, raw)]);
    expect(chips.map((c) => [c.attempt.number, c.tone, c.href, c.head])).toEqual([
      [1, "bad", "/jobs/r-1", false], [2, "q", "/jobs/r-2", false], [3, "bad", "/jobs/r-3", true]]);
    expect(chips[0].title).toBe("A1 · failed · bootstrap missing — Pipeline aborted: missing bootstrap");
    expect(chips[1].title).toBe("A2 · superseded");
    expect(chips[2].title).toBe("A3 · failed — overloaded");
  });

  it("attemptChips_OnlyOneAttempt_IsEmpty", () => {
    expect(attemptChips([failed(1)])).toEqual([]);
  });

  it("attemptWords_EndKindSameAsPill_IsNotRepeated", () => {
    expect(attemptWords(attempt({ status: "cancelled", endKind: "superseded" }))).toBe("superseded");
  });

  it("shortRepo_DottedAndSlashedNames_KeepTheLastSegment", () => {
    expect(shortRepo("Sample.App.Server")).toBe("Server");
    expect(shortRepo("org/Sample.App.Client")).toBe("Client");
    expect(shortRepo("o/api")).toBe("api");
    expect(shortRepo("api")).toBe("api");
  });
});
