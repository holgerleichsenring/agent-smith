import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// 2026-11-24-0fbfb: the spend read follows its key — read again when the key changes, never on an equal one; a failed
// read keeps what was read before and says it failed only when nothing was.

const fetchRunSpend = vi.fn();
vi.mock("@/lib/runSpendApi", async (actual) => ({
  ...(await actual<object>()),
  fetchRunSpend: (...args: unknown[]) => fetchRunSpend(...args),
}));

import { spendKey, useRunSpend } from "@/hooks/useRunSpend";

const spend = (totalUsd: number) => ({
  from: "", totalUsd, work: { costUsd: totalUsd, runs: 1 }, system: { costUsd: 0, runs: 0 }, llmCalls: 0, slices: [],
});

function run(runId: string, costUsd: number): RunSnapshot {
  return { runId, costUsd } as RunSnapshot;
}

const overview = (active: RunSnapshot[], recent: RunSnapshot[] = []) =>
  ({ active, recent, systemActivity: null }) as OverviewSnapshot;

beforeEach(() => fetchRunSpend.mockReset());

describe("useRunSpend", () => {
  it("UseRunSpend_KeyChanges_ReadsAgain_EqualKeyDoesNot", async () => {
    fetchRunSpend.mockResolvedValueOnce(spend(1)).mockResolvedValueOnce(spend(2));
    const { result, rerender } = renderHook(({ key }) => useRunSpend("today", key), { initialProps: { key: "a" } });
    await waitFor(() => expect(result.current.spend?.totalUsd).toBe(1));
    rerender({ key: "a" });
    expect(fetchRunSpend).toHaveBeenCalledTimes(1);
    rerender({ key: "b" });
    await waitFor(() => expect(result.current.spend?.totalUsd).toBe(2));
  });

  it("UseRunSpend_FailureAfterARead_KeepsTheReadAndIsNotFailed", async () => {
    fetchRunSpend.mockResolvedValueOnce(spend(3)).mockRejectedValueOnce(new Error("down"));
    const { result, rerender } = renderHook(({ key }) => useRunSpend("week", key), { initialProps: { key: "a" } });
    await waitFor(() => expect(result.current.spend?.totalUsd).toBe(3));
    rerender({ key: "b" });
    await waitFor(() => expect(fetchRunSpend).toHaveBeenCalledTimes(2));
    expect(result.current).toEqual({ spend: spend(3), failed: false });
  });

  it("UseRunSpend_FirstReadFails_IsFailed", async () => {
    fetchRunSpend.mockRejectedValueOnce(new Error("down"));
    const { result } = renderHook(() => useRunSpend("today", "a"));
    await waitFor(() => expect(result.current.failed).toBe(true));
  });
});

describe("spendKey", () => {
  it("SpendKey_MovesWithARunsCostOrANewRun_NotWithAnEqualOverview", () => {
    const before = spendKey(overview([run("a", 1)]));
    expect(spendKey(overview([run("a", 1)]))).toBe(before);
    expect(spendKey(overview([run("a", 1.5)]))).not.toBe(before);
    expect(spendKey(overview([run("a", 1), run("b", 0)]))).not.toBe(before);
    expect(spendKey(overview([], [run("a", 1)]))).not.toBe(before);
    expect(spendKey(null)).toBe("");
  });
});
