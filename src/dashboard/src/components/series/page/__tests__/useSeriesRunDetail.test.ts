import { act, renderHook } from "@testing-library/react";
import { HubConnectionState } from "@microsoft/signalr";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { seriesDetail } from "./seriesFixtures";

// 2026-11-24-a4e7: the series page reads its series again when the host moves one of its pull requests — a
// TicketActivityChanged of the series' project, a burst within 2 s costing one read — and after a reconnect.

const fetchSeriesRun = vi.hoisted(() => vi.fn());
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRun: (...args: unknown[]) => fetchSeriesRun(...args) }));

const { useSeriesRunDetail, SERIES_TICKET_NUDGE_MS } = await import("../useSeriesRunDetail");

const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers();
  fetchSeriesRun.mockReset().mockResolvedValue(seriesDetail());
});
afterEach(() => vi.useRealTimers());

describe("useSeriesRunDetail", () => {
  it("UseSeriesRunDetail_ABurstOfTheProjectsTicketNudges_ReadsOnceWithinTheWindow", async () => {
    const { hub, client } = fakeHub();
    const { result } = renderHook(() => useSeriesRunDetail("sr-1", client));
    await wait(0);
    expect(result.current.read.kind).toBe("read");
    const project = seriesDetail().project;

    for (let i = 0; i < 5; i++) act(() => hub.ticketActivityChanged.emit({ project, ticketKey: `github-${i}` }));
    await wait(SERIES_TICKET_NUDGE_MS - 1);
    expect(fetchSeriesRun).toHaveBeenCalledTimes(1);
    await wait(1);
    expect(fetchSeriesRun).toHaveBeenCalledTimes(2);
  });

  it("UseSeriesRunDetail_AnotherProjectsNudge_ReadsNothing", async () => {
    const { hub, client } = fakeHub();
    renderHook(() => useSeriesRunDetail("sr-1", client));
    await wait(0);

    act(() => hub.ticketActivityChanged.emit({ project: "elsewhere", ticketKey: "github-1" }));
    await wait(SERIES_TICKET_NUDGE_MS * 5);
    expect(fetchSeriesRun).toHaveBeenCalledTimes(1);
  });

  it("UseSeriesRunDetail_AReconnect_ReadsAgain", async () => {
    const { hub, client } = fakeHub();
    renderHook(() => useSeriesRunDetail("sr-1", client));
    await wait(0);

    // fakeHub's subject is typed by number; the hub emits the enum's values.
    const emit = (state: HubConnectionState) => act(() => hub.connectionState.emit(state as unknown as number));
    emit(HubConnectionState.Reconnecting);
    emit(HubConnectionState.Connected);
    await wait(0);
    expect(fetchSeriesRun).toHaveBeenCalledTimes(2);
  });
});
