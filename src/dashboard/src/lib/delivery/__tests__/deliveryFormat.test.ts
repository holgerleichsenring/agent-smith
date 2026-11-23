import { describe, expect, it } from "vitest";
import {
  CAUSES, KPIS, causeLabel, formatDelta, formatDuration, formatSpan, formatValue, formatWork, outcomeLabel, wording,
} from "../deliveryFormat";

// 2026-10-25-0099c: how the Delivery page writes its numbers and words.

const kpi = (id: string) => KPIS.find((k) => k.id === id)!;
const en = wording("en");
const de = wording("de");

describe("deliveryFormat", () => {
  it("Span_DatesForAWeekOrMonth_HoursForADay", () => {
    expect(formatSpan("week", "2026-11-05T14:00:01Z", "2026-11-12T14:00:01Z")).toBe("11-05 → 11-12");
    expect(formatSpan("day", "2026-11-11T14:00:01Z", "2026-11-12T14:00:01Z")).toBe("11-11 14:00 → 11-12 14:00");
  });

  it("Duration_MinutesUnderAnHour_HoursUnderTwoDays_ElseDays", () => {
    expect(formatDuration(en, 1500)).toBe("25 min");
    expect(formatDuration(en, 5400)).toBe("1.5 h");
    expect(formatDuration(en, 3 * 86400)).toBe("3.0 d");
  });

  it("Delta_SignedAndJudgedByTheBetterDirectionOfItsFigure", () => {
    expect(formatDelta(en, kpi("ticketsWorked"), 5, 2)).toEqual({ text: "+3", better: true });
    expect(formatDelta(en, kpi("mergeRate"), 0.56, 0.5)).toEqual({ text: "+6 pt", better: true });
    expect(formatDelta(en, kpi("medianTimeToMerge"), 3600, 18720)).toEqual({ text: "−4.2 h", better: true });
    expect(formatDelta(en, kpi("spend"), 30, 17.6)).toEqual({ text: "+$12.40", better: false });
    expect(formatDelta(en, kpi("spend"), 3, 3)).toEqual({ text: "no change", better: null });
    expect(formatDelta(en, kpi("mergeRate"), 0.5, null)).toBeNull();
  });

  it("Value_NullIsADash_NeverAnEmDash", () => {
    for (const k of KPIS) expect(formatValue(en, k.kind, null)).toBe("-");
  });

  it("Labels_EveryOutcomeAndEveryCauseTheServerSends", () => {
    const outcomes = ["running", "queued", "waiting", "delivered", "shortfall", "cancelled", "failed"] as const;
    const causes = ["success", "shortfall", "failed", "cancelled", "image-untrusted", "preflight", "bootstrap-missing", "criteria-unmet", "superseded"];
    for (const c of causes) expect(CAUSES as readonly string[], c).toContain(c);
    expect(causeLabel(en.t, "something-new")).toBe("something-new");
    for (const w of [en, de]) {
      const labels = [...outcomes.map((o) => outcomeLabel(w.t, o)), ...CAUSES.map((c) => causeLabel(w.t, c))];
      for (const label of labels) expect(label).not.toContain("—");
    }
  });

  it("Work_SpecsReposAndTheFirstGoal", () => {
    expect(formatWork(en.t, ["Search pages by cursor", "Client reads pages"], ["api"])).toBe("2 specs · 1 repo · Search pages by cursor");
    expect(formatWork(en.t, [], [])).toBe("-");
  });

  // 2026-11-23-5669f: German words and a decimal comma when German is chosen.
  it("German_WordsTheFiguresAndUsesADecimalComma", () => {
    expect(formatDuration(de, 5400)).toBe("1,5 h");
    expect(formatDuration(de, 3 * 86400)).toBe("3,0 Tage");
    expect(formatDelta(de, kpi("spend"), 30, 17.6)).toEqual({ text: "+12,40 $", better: false });
    expect(formatDelta(de, kpi("mergeRate"), 0.56, 0.5)).toEqual({ text: "+6 Pp.", better: true });
    expect(formatWork(de.t, ["Search pages by cursor"], ["api", "web"])).toBe("1 Spec · 2 Repos · Search pages by cursor");
  });
});
