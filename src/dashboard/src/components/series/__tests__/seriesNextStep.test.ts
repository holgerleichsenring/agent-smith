import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { SeriesRunRow } from "@/types/seriesRuns";
import { seriesEnded, seriesNextStep, seriesStateWord } from "../seriesNextStep";

// 2026-11-23-31dcc: a question first, then the ready merge, then stuck tickets, then the series' own end.

const t = translator("en");
const base: SeriesRunRow = {
  id: "sr-1", name: "n", project: "p", state: "running",
  counts: { total: 3, landed: 0, waiting: 0, held: 0, notStarted: 3 }, nextMerge: null, question: null, lastActivityAt: "",
};

describe("seriesNextStep", () => {
  it.each([
    [{ question: "Keep it?", nextMerge: "#1" }, "A question is waiting for you"],
    [{ nextMerge: "#1" }, "Merge #1 next"],
    [{ counts: { ...base.counts, held: 2 } }, "2 tickets wait on someone"],
    [{ state: "done" }, "Every ticket is merged."],
    [{ state: "stopped", nextMerge: "#1" }, "Stopped."],
    [{}, "Working. Nothing for you to do."],
  ])("seriesNextStep_%o", (over, sentence) => {
    expect(seriesNextStep({ ...base, ...over } as SeriesRunRow, t)).toBe(sentence);
  });

  it("seriesStateWord_KnownAndUnknown", () => {
    expect(seriesStateWord("verifying", t)).toBe("checking");
    expect(seriesStateWord("delivering", t)).toBe("merging into the default branch"); // 2026-11-24-f9c5e
    expect(seriesStateWord("odd", t)).toBe("odd");
    expect(seriesStateWord("paused", translator("de"))).toBe("pausiert");
  });
});

// 2026-11-26-5201d: what the list hides until Show ended.
describe("seriesEnded", () => {
  it("SeriesEnded_DoneAndStopped_AreEnded_TheRestMoves", () => {
    expect(["done", "stopped"].every(seriesEnded)).toBe(true);
    expect(["running", "verifying", "delivering", "paused"].some(seriesEnded)).toBe(false);
  });
});
