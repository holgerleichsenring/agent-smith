import { describe, expect, it } from "vitest";
import {
  CAUSE_LABEL, KPIS, OUTCOME_LABEL, causeLabel, formatDelta, formatDuration, formatSpan, formatValue, formatWork,
} from "../deliveryFormat";

// 2026-10-25-0099c: how the Delivery page writes its numbers and words.

const kpi = (label: string) => KPIS.find((k) => k.label === label)!;

describe("deliveryFormat", () => {
  it("Span_DatesForAWeekOrMonth_HoursForADay", () => {
    expect(formatSpan("week", "2026-11-05T14:00:01Z", "2026-11-12T14:00:01Z")).toBe("11-05 → 11-12");
    expect(formatSpan("day", "2026-11-11T14:00:01Z", "2026-11-12T14:00:01Z")).toBe("11-11 14:00 → 11-12 14:00");
  });

  it("Duration_MinutesUnderAnHour_HoursUnderTwoDays_ElseDays", () => {
    expect(formatDuration(1500)).toBe("25 min");
    expect(formatDuration(5400)).toBe("1.5 h");
    expect(formatDuration(3 * 86400)).toBe("3.0 d");
  });

  it("Delta_SignedAndJudgedByTheBetterDirectionOfItsFigure", () => {
    expect(formatDelta(kpi("Tickets worked"), 5, 2)).toEqual({ text: "+3", better: true });
    expect(formatDelta(kpi("Merge rate"), 0.56, 0.5)).toEqual({ text: "+6 pt", better: true });
    expect(formatDelta(kpi("Median time to merge"), 3600, 18720)).toEqual({ text: "−4.2 h", better: true });
    expect(formatDelta(kpi("Spend"), 30, 17.6)).toEqual({ text: "+$12.40", better: false });
    expect(formatDelta(kpi("Spend"), 3, 3)).toEqual({ text: "no change", better: null });
    expect(formatDelta(kpi("Merge rate"), 0.5, null)).toBeNull();
  });

  it("Value_NullIsADash_NeverAnEmDash", () => {
    for (const k of KPIS) expect(formatValue(k.kind, null)).toBe("-");
  });

  it("Labels_EveryOutcomeAndEveryCauseTheServerSends", () => {
    const outcomes = ["running", "queued", "waiting", "delivered", "shortfall", "cancelled", "failed"] as const;
    for (const o of outcomes) expect(OUTCOME_LABEL[o]).toBeTruthy();
    const causes = ["success", "shortfall", "failed", "cancelled", "image-untrusted", "preflight", "bootstrap-missing", "criteria-unmet", "superseded"];
    for (const c of causes) expect(CAUSE_LABEL[c], c).toBeTruthy();
    expect(causeLabel("something-new")).toBe("something-new");
    for (const label of [...Object.values(OUTCOME_LABEL), ...Object.values(CAUSE_LABEL)]) expect(label).not.toContain("—");
  });

  it("Work_SpecsReposAndTheFirstGoal", () => {
    expect(formatWork(["Search pages by cursor", "Client reads pages"], ["api"])).toBe("2 specs · 1 repo · Search pages by cursor");
    expect(formatWork([], [])).toBe("-");
  });
});
