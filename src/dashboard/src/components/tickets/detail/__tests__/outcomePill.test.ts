import { describe, expect, it } from "vitest";
import { attemptWords, outcomePill, outcomeTone } from "../attemptOutcome";
import { attempt } from "./detailFixtures";

// 2026-10-16-86e6b: a park closed because a newer attempt started past it reads superseded, before any other mark.

describe("outcomePill", () => {
  it("outcomePill_EndKindSuperseded_ReadsSuperseded", () => {
    expect(outcomePill(attempt({ status: "cancelled", endKind: "superseded", handedBack: true }))).toEqual(["q", "superseded"]);
  });

  // 2026-11-19-1b72a: a start its paused series held is quiet, not a failure.
  it("outcomePill_SeriesPaused_IsQuietAndSaysTheSeriesHeldIt", () => {
    const held = attempt({ status: "cancelled", endKind: "series-paused" });
    expect(outcomePill(held)).toEqual(["q", "held while the series is paused"]);
    expect(attemptWords(held)).toBe("held while the series is paused");
    expect(outcomeTone(held)).toBe("quiet");
  });

  it("outcomePill_NoEndKind_KeepsTheStatusPill", () => {
    expect(outcomePill(attempt({ status: "failed" }))).toEqual(["bad", "failed"]);
  });

  // 2026-11-17-8d1cb: every attempt names its end kind beside its outcome when that adds something.
  it("attemptWords_AddsTheEndKind_OnlyWhenItSaysMoreThanTheOutcome", () => {
    expect(attemptWords(attempt({ status: "failed", endKind: "image-untrusted" }))).toBe("failed · image not trusted");
    expect(attemptWords(attempt({ status: "cancelled", endKind: "superseded" }))).toBe("superseded");
    expect(attemptWords(attempt({ status: "success" }))).toBe("succeeded");
  });

  it("outcomeTone_ColoursByOutcome", () => {
    expect(outcomeTone(attempt({ status: "success" }))).toBe("ok");
    expect(outcomeTone(attempt({ status: "failed" }))).toBe("bad");
    expect(outcomeTone(attempt({ status: "running", finishedAt: null }))).toBe("run");
    expect(outcomeTone(attempt({ status: "success", handedBack: true }))).toBe("person");
    expect(outcomeTone(attempt({ status: "cancelled" }))).toBe("quiet");
  });
});
