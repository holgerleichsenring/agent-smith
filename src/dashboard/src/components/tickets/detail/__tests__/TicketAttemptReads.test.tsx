import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, compute, conversation, detail, passes, runDetail, series, spec, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";

// 2026-10-13-9a59s: the Attempts and Cost tabs read over HTTP through the page store's own bindings — the
// run detail through runsApi.fetchRun, the series through q's store — one request per endpoint and run.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const nav = vi.hoisted(() => ({ search: "tab=attempts" }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(nav.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));
const http = vi.hoisted(() => ({
  fetchRun: vi.fn(),
  fetchTicketDetail: vi.fn(),
  fetchTicketSeries: vi.fn(),
  fetchTicketConversation: vi.fn(),
  fetchTicketText: vi.fn(),
  fetchRunPasses: vi.fn(),
  fetchRunCompute: vi.fn(),
  fetchApprovalPreview: vi.fn(),
  retryAttempt: vi.fn(),
}));
vi.mock("@/lib/runsApi", () => ({ fetchRun: http.fetchRun }));
vi.mock("@/lib/ticketsApi", () => http);

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

beforeEach(() => {
  vi.useFakeTimers();
  const attempts = [
    attempt({ number: 1, runId: "r-1", status: "failed", handedBack: true }),
    attempt({ number: 2, runId: "r-2", status: "success" }),
  ];
  http.fetchTicketDetail.mockResolvedValue(detail(attempts));
  http.fetchTicketSeries.mockImplementation((_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-2", [spec()])));
  http.fetchTicketConversation.mockResolvedValue(conversation([]));
  http.fetchTicketText.mockResolvedValue(ticketText());
  http.fetchRunPasses.mockImplementation((runId: string) => Promise.resolve(passes(runId, {})));
  http.fetchRunCompute.mockImplementation((runId: string) => Promise.resolve(compute(runId)));
  http.fetchRun.mockImplementation((runId: string) => Promise.resolve(runDetail(runId)));
});
afterEach(() => {
  vi.useRealTimers();
  Object.values(http).forEach((fn) => fn.mockReset());
});

describe("Ticket attempt reads", () => {
  it("TheRunReadGoesThroughFetchRun_AndTheSeriesReadThroughTheStore_OneRequestPerEndpoint", async () => {
    render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client}><TicketPage /></TicketPageStore>);
    for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(0));
    expect(screen.getByTestId("attempts-timeline")).toBeInTheDocument();
    expect(http.fetchRun.mock.calls.map((c) => c[0]).sort()).toEqual(["r-1", "r-2"]);
    expect(http.fetchRunCompute.mock.calls.map((c) => c[0]).sort()).toEqual(["r-1", "r-2"]);
    expect(http.fetchRunPasses.mock.calls.map((c) => c[0]).sort()).toEqual(["r-1", "r-2"]);
    // the default cut once, and the handed-back attempt's own cut once — nothing else re-reads the series
    expect(http.fetchTicketSeries.mock.calls.map((c) => c[2])).toEqual([null, "r-1"]);
    expect(http.fetchTicketDetail).toHaveBeenCalledTimes(1);
  });
});
