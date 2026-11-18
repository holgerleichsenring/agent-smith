import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { attempt, detail } from "@/components/tickets/detail/__tests__/detailFixtures";
import type { RunSnapshot } from "@/types/hub-events";
import type { TicketDetail } from "@/types/tickets";

// 2026-11-18-d6d2d: the run page's ticket names the latest attempt and reads again when the ticket's nudge arrives,
// so an older attempt links to the latest and a requested Retry can follow the new one.

const { hub, client } = fakeHub();
vi.mock("@/lib/JobsHubClient", () => ({ getJobsHubClient: () => client }));
let world: TicketDetail;
const fetchTicketDetail = vi.fn(() => Promise.resolve(world));
vi.mock("@/lib/ticketsApi", () => ({ fetchTicketDetail: () => fetchTicketDetail() }));

const { useRunTicket } = await import("../useRunTicket");

const snapshot = { runId: "r-1", project: "shop", ticketId: "101", status: "failed" } as unknown as RunSnapshot;
const failed = attempt({ status: "failed" });
const second = attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null });

describe("useRunTicket", () => {
  beforeEach(() => {
    fetchTicketDetail.mockClear();
    world = detail([failed]);
  });

  it("useRunTicket_TheOnlyAttempt_IsTheNewestAndTheLatest", async () => {
    const { result } = renderHook(() => useRunTicket("r-1", snapshot));
    await waitFor(() => expect(result.current?.latest?.runId).toBe("r-1"));
    expect(result.current?.newest).toBe(true);
    expect(result.current?.label).toBe("GH-101");
  });

  it("useRunTicket_TheTicketsNudge_ReadsAgain_AndNamesTheNewerAttempt", async () => {
    const { result } = renderHook(() => useRunTicket("r-1", snapshot));
    await waitFor(() => expect(result.current?.newest).toBe(true));
    world = detail([failed, second]);
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await waitFor(() => expect(result.current?.latest?.runId).toBe("r-2"));
    expect(result.current?.newest).toBe(false);
  });

  it("useRunTicket_AnotherTicketsNudge_ReadsNothing", async () => {
    const { result } = renderHook(() => useRunTicket("r-1", snapshot));
    await waitFor(() => expect(result.current?.newest).toBe(true));
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-7" }));
    expect(fetchTicketDetail).toHaveBeenCalledTimes(1);
  });

  it("useRunTicket_Reread_ReadsTheTicketAgain", async () => {
    const { result } = renderHook(() => useRunTicket("r-1", snapshot));
    await waitFor(() => expect(result.current?.newest).toBe(true));
    world = detail([failed, second]);
    act(() => result.current?.reread());
    await waitFor(() => expect(result.current?.latest?.number).toBe(2));
  });
});
