import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "@/lib/apiResponse";
import { seriesDetail } from "./seriesFixtures";

// 2026-11-23-31dcd: the series page shows one section at a time, named by ?section=; its Tickets rows run by depth and
// open the ticket page; its Conversation posts to the series' own conversation. 2026-11-23-f562: three sections, the
// Overview first; the retired names land on it; Changed in this series is read under Tickets.

const hoisted = vi.hoisted(() => ({
  search: "", replace: vi.fn(), push: vi.fn(), fetch: vi.fn(), changes: vi.fn(),
  dialog: { open: vi.fn(), send: vi.fn(), attach: vi.fn(), entries: [], working: false, readings: [], activity: [],
    workingSince: null, uploadNote: null, failure: null, awaitingProposal: null, question: null,
    view: { session: { sessionId: "conv-1" } } as unknown },
}));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: hoisted.replace, push: hoisted.push }),
}));
vi.mock("@/lib/seriesRunsApi", () => ({
  fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a),
  fetchSeriesRunChanges: (...a: unknown[]) => hoisted.changes(...a), // 2026-11-23-31dcj
}));
vi.mock("@/hooks/useSpecDialog", () => ({ useSpecDialog: () => hoisted.dialog }));

import { SeriesRunPage } from "../SeriesRunPage";

beforeEach(() => {
  hoisted.search = "";
  hoisted.replace.mockReset();
  hoisted.push.mockReset();
  hoisted.dialog.open.mockReset();
  hoisted.dialog.send.mockReset();
  hoisted.fetch.mockReset().mockResolvedValue(seriesDetail());
  hoisted.changes.mockReset().mockResolvedValue({ changes: [], unread: [] });
});

describe("SeriesRunPage", () => {
  it("SeriesPage_SelectingASection_ShowsOnlyItsPane_AndSetsTheSectionParam", async () => {
    render(<SeriesRunPage id="sr-1" />);
    expect(await screen.findByTestId("series-pane-overview")).toBeInTheDocument();
    expect(screen.queryByTestId("series-pane-talk")).toBeNull();
    expect(screen.getAllByTestId(/^series-section-(overview|tickets|talk)$/)).toHaveLength(3);
    expect(screen.queryByTestId("series-your-step")).toBeNull();

    fireEvent.click(screen.getByTestId("series-section-talk"));

    expect(hoisted.replace).toHaveBeenCalledWith("/series/sr-1?section=talk", { scroll: false });
  });

  it("SeriesPage_SectionParam_ShowsThatSectionAlone", async () => {
    hoisted.search = "section=talk";
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-pane-talk")).toBeInTheDocument();
    expect(screen.queryByTestId("series-pane-tickets")).toBeNull();
    expect(screen.getByTestId("series-section-title")).toHaveTextContent("Conversation");
  });

  it("SeriesPage_RetiredSectionNames_LandOnTheOverview", async () => {
    for (const name of ["next", "held", "course"]) {
      hoisted.search = `section=${name}`;
      const page = render(<SeriesRunPage id="sr-1" />);
      expect(await screen.findByTestId("series-pane-overview")).toBeInTheDocument();
      expect(screen.getByTestId("series-section-title")).toHaveTextContent("Overview");
      page.unmount();
    }
  });

  it("SeriesPage_ChangedInThisSeries_IsReadOnlyOnceTicketsOpens", async () => {
    const first = render(<SeriesRunPage id="sr-1" />);
    expect(await screen.findByTestId("series-pane-overview")).toBeInTheDocument();
    expect(hoisted.changes).not.toHaveBeenCalled();
    first.unmount();

    hoisted.search = "section=tickets";
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-changes-empty")).toBeInTheDocument();
    expect(hoisted.changes).toHaveBeenCalledTimes(1);
    expect(hoisted.changes).toHaveBeenCalledWith("sr-1", expect.any(AbortSignal));
  });

  it("SeriesPage_TicketsRows_SortByDepth_AndOpenTheTicketPage", async () => {
    hoisted.search = "section=tickets";
    render(<SeriesRunPage id="sr-1" />);
    const table = await screen.findByTestId("series-tickets");

    const keys = [...table.querySelectorAll("tbody tr")].map((row) => row.querySelector("a")?.textContent);
    expect(keys).toEqual(["#48", "#49", "#50"]);
    expect(screen.getByTestId("series-ticket-49")).toHaveTextContent("PR #9935 waiting");
    expect(screen.getByTestId("series-ticket-48")).toHaveTextContent("merged");
    fireEvent.click(screen.getByTestId("series-ticket-50"));
    expect(hoisted.push).toHaveBeenCalledWith("/tickets/shop/50");
  });

  it("SeriesPage_Conversation_PostsToTheSeriesOwnConversation", async () => {
    hoisted.search = "section=talk";
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-talk"); // that it lands on the thread: SeriesConversationSection.test.tsx, real hook

    fireEvent.change(screen.getByTestId("dialog-composer-text"), { target: { value: "The worker needs the callback URL" } });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() => expect(hoisted.dialog.send).toHaveBeenCalledWith("The worker needs the callback URL", "shop"));
  });

  it("SeriesPage_AClosedConversation_IsContinuedInWorkItOut", async () => {
    hoisted.search = "section=talk";
    hoisted.fetch.mockResolvedValue(seriesDetail({ conversationOpen: false }));
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-talk-closed")).toHaveTextContent("This conversation is closed.");
    expect(hoisted.dialog.open).not.toHaveBeenCalled();
  });

  it("SeriesPage_UnknownSeries_SaysSo", async () => {
    hoisted.fetch.mockRejectedValue(new ApiResponseError("/api/series-runs/x", 404, "HTTP 404"));
    render(<SeriesRunPage id="x" />);

    expect(await screen.findByTestId("series-page-unread")).toHaveTextContent("There is no such series");
  });
});
