import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { delivery } from "@/components/delivery/__tests__/deliveryFixtures";

// 2026-10-25-0099c: the Delivery read — once on mount, again on a period change, a RunsChanged burst within 15 s
// and a TicketActivityChanged within 2 s, each burst one read.

const fetchDelivery = vi.hoisted(() => vi.fn());
vi.mock("@/lib/deliveryApi", () => ({ fetchDelivery: (...args: unknown[]) => fetchDelivery(...args) }));

const { useDelivery } = await import("../useDelivery");

const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers();
  fetchDelivery.mockReset().mockResolvedValue(delivery());
});
afterEach(() => vi.useRealTimers());

describe("useDelivery", () => {
  it("ABurstOfRunsChanged_CostsOneReadWithinFifteenSeconds", async () => {
    const { hub, client } = fakeHub();
    renderHook(() => useDelivery("week", [], client));
    await wait(0);
    expect(fetchDelivery).toHaveBeenCalledTimes(1);

    for (let i = 0; i < 10; i++) act(() => hub.runsChanged.emit(`r-${i}`));
    await wait(14999);
    expect(fetchDelivery).toHaveBeenCalledTimes(1);
    await wait(1);
    expect(fetchDelivery).toHaveBeenCalledTimes(2);
  });

  it("TicketActivityChanged_ReadsWithinTwoSeconds", async () => {
    const { hub, client } = fakeHub();
    renderHook(() => useDelivery("week", [], client));
    await wait(0);

    act(() => hub.runsChanged.emit("r-1"));
    act(() => hub.ticketActivityChanged.emit({ project: "web-shop", ticketKey: "github-101" }));
    await wait(2000);
    expect(fetchDelivery).toHaveBeenCalledTimes(2);
    await wait(20000);
    expect(fetchDelivery).toHaveBeenCalledTimes(2);
  });

  it("APeriodOrProjectChange_ReadsAgainWithIt", async () => {
    const { client } = fakeHub();
    const { result, rerender } = renderHook(({ period, projects }) => useDelivery(period, projects, client), {
      initialProps: { period: "week" as const as "week" | "day", projects: [] as string[] },
    });
    await wait(0);
    expect(result.current.view?.period).toBe("week");

    rerender({ period: "day", projects: ["web-shop"] });
    await wait(0);
    expect(fetchDelivery).toHaveBeenLastCalledWith("day", ["web-shop"], expect.any(AbortSignal));
  });
});
