import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-f562: the series graph — one colour per ticket, the legend, the course select. 2026-11-25-9992j: a picture to
// read, nothing to operate — a working ticket carries the working mark, the host's conflict and CI show as marks, and a
// click opens read-only details (state, pull requests with CI, cost, the ticket's page) with no button in them.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn(), search: "" }));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a) }));
vi.mock("@/hooks/useSpecDialog", () => ({
  useSpecDialog: () => ({ entries: [], working: false, readings: [], activity: [], workingSince: null, uploadNote: null, failure: null,
    awaitingProposal: null, question: null, view: { session: { sessionId: "conv-1" } }, send: vi.fn(), attach: vi.fn() }),
}));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

import { SeriesRunPage } from "../SeriesRunPage";

beforeEach(() => {
  hoisted.fetch.mockReset().mockResolvedValue(overviewDetail());
  hoisted.search = "";
});

const open = async (ticketId: string) => {
  fireEvent.click(await screen.findByTestId(`series-node-${ticketId}`));
  return screen.getByTestId(`series-details-${ticketId}`);
};

describe("SeriesOverviewSection", () => {
  it("Overview_EachNode_HasOneColour_AndTheLegendNamesEveryColour", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const kinds = Object.fromEntries((await screen.findAllByTestId(/^series-node-/)).map((n) => [n.dataset.testid, n.dataset.kind]));

    expect(kinds["series-node-50"]).toBe("now");
    expect(kinds["series-node-51"]).toBe("needed");
    expect(kinds["series-node-61"]).toBe("question");
    expect(screen.getByTestId("series-graph-legend").querySelectorAll("span")).toHaveLength(8);
    expect(screen.getByTestId("series-edge-50>51")).toHaveClass("needed");
    expect(screen.getByTestId("series-edge-50>61")).not.toHaveClass("needed");
  });

  it("Overview_HasNoActsChipsOrLists", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-node-50");

    for (const gone of ["series-chips", "series-action-list", "series-list-yours", "series-list-needed", "series-act-outcome"])
      expect(screen.queryByTestId(gone)).toBeNull();
    expect(within(screen.getByTestId("series-graph")).queryAllByRole("button").every((b) => b.classList.contains("sr-node"))).toBe(true);
    const acts = screen.queryAllByRole("button").filter((b) => !b.classList.contains("sr-node"));
    expect(acts.filter((b) => /merge|retry|try again|hold/i.test(b.textContent ?? ""))).toHaveLength(0);
  });

  it("Graph_AWorkedOnTicket_CarriesTheWorkingMark_FromItsFlagOrWorkingOn", async () => {
    const detail = overviewDetail();
    detail.workingOn = ["#63", "#56"]; // #63 also says working itself; #56 only through the series' workingOn
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-node-63")).toHaveClass("working");
    expect(screen.getByTestId("series-node-56")).toHaveClass("working");
    expect(screen.getByTestId("series-node-56")).toHaveAccessibleName(/working on it now/);
    expect(screen.getByTestId("series-node-50")).not.toHaveClass("working");
  });

  it("Graph_RedCiAndAConflict_ShowTheirMarks", async () => {
    const detail = overviewDetail();
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "56");
    detail.tickets[at] = { ...detail.tickets[at], ci: "red",
      pullRequests: detail.tickets[at].pullRequests.map((pr) => ({ ...pr, ci: "red", hasConflicts: true })) };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-mark-conflict-56")).toHaveAttribute("title", "Conflict");
    expect(screen.getByTestId("series-mark-ciRed-56")).toHaveAttribute("title", "CI red");
    expect(screen.queryByTestId("series-mark-ciRed-50")).toBeNull();
  });

  it("Details_ShowStatePullRequestsCiAndCost_AndNoButton", async () => {
    const detail = overviewDetail();
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "56");
    detail.tickets[at] = { ...detail.tickets[at], costUsd: 3.4 };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);
    const details = await open("56");

    expect(within(details).getByTestId("series-details-state-56")).toHaveTextContent("PR waiting");
    const pr = within(details).getByTestId("series-details-pr-client-56");
    expect(within(pr).getByRole("link")).toHaveAttribute("href", "https://git.test/client/pull/9980");
    expect(pr).toHaveTextContent("CI green");
    expect(within(details).getByTestId("series-details-cost-56")).toHaveTextContent("$3.40");
    expect(within(details).getByTestId("series-details-open-56")).toHaveAttribute("href", "/tickets/shop/56");
    expect(within(details).queryAllByRole("button")).toHaveLength(0);
  });

  it("CourseSelect_LightsTheChosenTicketsCourse_AndAllLightsEverything", async () => {
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.change(await screen.findByTestId("series-graph-from"), { target: { value: "61" } });

    expect(screen.getByTestId("series-node-56")).toHaveClass("dim");
    expect(screen.getByTestId("series-node-48")).not.toHaveClass("dim");
    fireEvent.change(screen.getByTestId("series-graph-from"), { target: { value: "" } });
    expect(screen.getByTestId("series-node-56")).not.toHaveClass("dim");
  });

  it("Details_CloseOnEscape_AndOnAClickOutside", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await open("50");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByTestId("series-details-50")).toBeNull();
    expect(screen.getByTestId("series-node-50")).toHaveFocus();

    await open("50");
    fireEvent.mouseDown(screen.getByTestId("series-graph-legend"));
    expect(screen.queryByTestId("series-details-50")).toBeNull();
  });
});
