import { describe, it, expect } from "vitest";
import { deriveCostRollup } from "@/hooks/useCostRollup";
import { spendFrom } from "@/hooks/useRunSpend";
import { localDayStart, weekStart, type RunSpend } from "@/lib/runSpendApi";

// 2026-11-24-0fbfb: the Overview's figures are the server's two spend reads — today since the viewer's local midnight,
// the week and its calls since seven days ago — never a sum over the overview's window.

function spend(totalUsd: number, llmCalls = 0): RunSpend {
  return {
    from: "", totalUsd, work: { costUsd: totalUsd, runs: 1 }, system: { costUsd: 0, runs: 0 }, llmCalls, slices: [],
  };
}

describe("deriveCostRollup", () => {
  it("CostRollup_BothRead_TodayAndWeekFromTheirReads_CallsFromTheWeek", () => {
    const cost = deriveCostRollup({ spend: spend(2.5, 3), failed: false }, { spend: spend(12.65, 211), failed: false });
    expect(cost).toEqual({ today: 2.5, week: 12.65, llmCalls: 211 });
  });

  it("CostRollup_OneNotRead_IsNull", () => {
    expect(deriveCostRollup({ spend: null, failed: false }, { spend: spend(1), failed: false })).toBeNull();
    expect(deriveCostRollup({ spend: spend(1), failed: false }, { spend: null, failed: true })).toBeNull();
  });
});

describe("spend windows", () => {
  it("LocalDayStart_IsTheViewersMidnight", () => {
    const now = new Date(2026, 10, 24, 15, 30).getTime();
    expect(new Date(localDayStart(now)).getTime()).toBe(new Date(2026, 10, 24, 0, 0).getTime());
    expect(spendFrom("today", now)).toBe(localDayStart(now));
  });

  it("WeekStart_IsSevenDaysBack", () => {
    const now = Date.parse("2026-11-24T12:00:00Z");
    expect(weekStart(now)).toBe("2026-11-17T12:00:00.000Z");
    expect(spendFrom("week", now)).toBe(weekStart(now));
  });
});
