import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import type { SeriesRunChanges } from "@/types/seriesRuns";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcj: Changed in this series — each revision after a ticket's first, in the server's order (newest first),
// with its ticket, number, cause and time; an unread branch is named; nothing to act on.

const hoisted = vi.hoisted(() => ({ changes: vi.fn() }));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRunChanges: (...a: unknown[]) => hoisted.changes(...a) }));

import { SeriesChangesNotes } from "../SeriesChangesNotes";

const detail = () => seriesDetail({
  tickets: [seriesTicket({ ticketId: "61", key: "#61" }), seriesTicket({ ticketId: "62", key: "#62" })],
});

const answer = (over: Partial<SeriesRunChanges> = {}): SeriesRunChanges => ({
  changes: [
    { ticketId: "61", revision: 3, cause: "revised in run run-7: the migration needs a rollback step", at: "2026-11-22T14:00:00Z" },
    { ticketId: "62", revision: 2, cause: "approved in design conversation conv-1", at: "2026-11-22T10:00:00Z" },
  ],
  unread: [], ...over,
});

beforeEach(() => hoisted.changes.mockReset());

describe("SeriesChangesNotes", () => {
  it("SeriesChanges_ListsEachRevision_NewestFirst_WithTicketCauseAndNoAct", async () => {
    hoisted.changes.mockResolvedValue(answer());
    render(<SeriesChangesNotes detail={detail()} />);

    const rows = await screen.findAllByTestId(/^series-change-\d+-\d+$/);
    expect(rows.map((row) => row.dataset.testid)).toEqual(["series-change-61-3", "series-change-62-2"]);
    expect(rows[0]).toHaveTextContent("rev 3");
    expect(rows[0]).toHaveTextContent("#61 revised in run run-7: the migration needs a rollback step");
    expect(rows[1]).toHaveTextContent("approved in design conversation conv-1");
    expect(screen.getByRole("link", { name: "#61" })).toHaveAttribute("href", "/tickets/shop/61");
    expect(screen.queryByRole("button")).toBeNull();
    expect(hoisted.changes).toHaveBeenCalledWith("sr-1", expect.any(AbortSignal));
  });

  it("SeriesChanges_NoRevisionAfterTheFirst_SaysSo", async () => {
    hoisted.changes.mockResolvedValue(answer({ changes: [] }));
    render(<SeriesChangesNotes detail={detail()} />);

    expect(await screen.findByTestId("series-changes-empty")).toBeInTheDocument();
  });

  it("SeriesChanges_AnUnreadBranch_IsNamed_AndAFailedReadSaysSo", async () => {
    hoisted.changes.mockResolvedValueOnce(answer({ changes: [], unread: [{ ticketId: "62", why: "503 from the host" }] }));
    const { unmount } = render(<SeriesChangesNotes detail={detail()} />);
    expect(await screen.findByTestId("series-change-unread-62")).toHaveTextContent("#62");
    expect(screen.getByTestId("series-change-unread-62")).toHaveTextContent("503 from the host");
    expect(screen.queryByTestId("series-changes-empty")).toBeNull();
    unmount();

    hoisted.changes.mockRejectedValueOnce(new Error("down"));
    render(<SeriesChangesNotes detail={detail()} />);
    expect(await screen.findByTestId("series-changes-failed")).toBeInTheDocument();
  });

  it("SeriesChanges_InGerman_SpeaksInformally", async () => {
    resetLanguage("de");
    hoisted.changes.mockResolvedValue(answer());
    render(<SeriesChangesNotes detail={detail()} />);
    expect(await screen.findByText("Geändert in dieser Serie")).toBeInTheDocument();
    expect(screen.getByText(/du musst nichts tun/)).toBeInTheDocument();
    expect(screen.getByTestId("series-change-61-3")).toHaveTextContent("Rev. 3");
  });
});
