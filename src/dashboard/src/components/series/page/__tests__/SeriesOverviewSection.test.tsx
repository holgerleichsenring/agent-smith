import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-23-f562: the series graph — one colour per ticket, the legend, the course select. 2026-11-25-9992j: a picture to
// read, nothing to operate — a working ticket carries the working mark, the host's conflict and CI show as marks, and a
// click opens read-only details (state, pull requests with CI, cost, the ticket's page) with no button in them.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn(), start: vi.fn(), search: "" }));
vi.mock("@/lib/seriesRunsApi", () => ({
  fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a),
  startSeriesTicket: (...a: unknown[]) => hoisted.start(...a),
}));
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
  // 2026-11-26-5201e: a node links its pull request and its tracker in a new tab, beside its button; the details too.
  it("Overview_ANode_LinksItsPullRequestAndTracker_InANewTab_OutsideItsButton", async () => {
    const detail = overviewDetail();
    detail.tickets = detail.tickets.map((ticket) => ticket.ticketId === "50" ? { ...ticket, webUrl: "https://tracker.test/items/50" } : ticket);
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);

    const node = await screen.findByTestId("series-node-50");
    const pr = screen.getByTestId("series-links-pr-50");
    const tracker = screen.getByTestId("series-links-tracker-50");
    expect(pr).toHaveAttribute("href", "https://git.test/server/pull/9937");
    expect(tracker).toHaveAttribute("href", "https://tracker.test/items/50");
    for (const link of [pr, tracker]) {
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("rel", "noreferrer");
      expect(node).not.toContainElement(link);
    }
    expect(screen.queryByTestId("series-links-tracker-48")).toBeNull();

    const details = await open("50");
    const inDetails = within(details).getByTestId("series-details-tracker-50");
    expect(inDetails).toHaveAttribute("href", "https://tracker.test/items/50");
    expect(inDetails).toHaveAttribute("target", "_blank");
    fireEvent.mouseDown(screen.getByTestId("series-links-pr-50"));
    expect(screen.getByTestId("series-details-50")).toBeInTheDocument(); // a node's own link leaves its details open
  });

  it("Overview_EachNode_HasOneColour_AndTheLegendNamesEveryColour", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const kinds = Object.fromEntries((await screen.findAllByTestId(/^series-node-/)).map((n) => [n.dataset.testid, n.dataset.kind]));

    expect(kinds["series-node-50"]).toBe("now");
    expect(kinds["series-node-51"]).toBe("needed");
    expect(kinds["series-node-61"]).toBe("question");
    expect(screen.getByTestId("series-graph-legend").querySelectorAll("span")).toHaveLength(10);
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

  // 2026-11-26-5201f: a pull request waiting for a person's approval reads as that person's step — the kind, the hold's
  // sentence, the PR's link and its word, the node's mark — not as CI running.
  it("Details_AnApprovalNeeded_SaysSoWithThePullRequest_NotCiRunning", async () => {
    const detail = overviewDetail();
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "56");
    const ticket = detail.tickets[at];
    detail.tickets[at] = { ...ticket, kind: "approval", ci: "green",
      pullRequests: ticket.pullRequests.map((pr) => ({ ...pr, approvalRequired: true })),
      hold: { addressee: "decide", case: "approval_needed", act: { kind: "pull_request", target: ticket.pullRequests[0].url }, since: null,
        attempt: 1, names: [] } };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);
    expect(await screen.findByTestId("series-mark-approval-56")).toHaveAttribute("title", "Needs your approval");
    expect(screen.queryByTestId("series-mark-ciRunning-56")).toBeNull();
    const details = await open("56");

    expect(within(details).getByTestId("series-details-state-56")).toHaveTextContent("needs your approval — Its pull request needs your approval on the host");
    expect(within(details).getByTestId("series-details-approval-client-56")).toHaveTextContent("needs your approval");
    expect(within(within(details).getByTestId("series-details-pr-client-56")).getByRole("link")).toHaveAttribute("href", "https://git.test/client/pull/9980");
  });

  // 2026-11-26-5201f addendum: a draft is its own kind and step — the hold's sentence and the PR's word, not CI or approval.
  it("Details_ADraft_SaysSo_ApartFromApprovalAndCi", async () => {
    const detail = overviewDetail();
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "56");
    const ticket = detail.tickets[at];
    detail.tickets[at] = { ...ticket, kind: "draft", ci: "pending",
      pullRequests: ticket.pullRequests.map((pr) => ({ ...pr, isDraft: true })),
      hold: { addressee: "decide", case: "pr_draft", act: { kind: "pull_request", target: ticket.pullRequests[0].url }, since: null,
        attempt: 1, names: [] } };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);
    expect((await screen.findByTestId("series-node-56")).dataset.kind).toBe("draft");
    const details = await open("56");

    expect(within(details).getByTestId("series-details-state-56")).toHaveTextContent("draft PR — Its pull request is still a draft");
    expect(within(details).getByTestId("series-details-draft-client-56")).toHaveTextContent("draft");
  });

  // 2026-11-25-9992e: the series master's newest act on the ticket — what, why, when — and none before it acted.
  // 2026-11-26-5f6e: a ticket no run worked yet — one the master added is listed nowhere else — is started here, and the
  // server's answer is shown as is; a worked ticket has no Start.
  it("Details_ANeverWorkedTicket_HasStart_AndShowsWhatTheStartCameTo", async () => {
    hoisted.start.mockResolvedValue({ kind: "refused", text: "Ticket 62 was not started: it waits for #51.", runId: null });
    render(<SeriesRunPage id="sr-1" />);
    const details = await open("62");

    fireEvent.click(within(details).getByTestId("series-details-start-62"));

    expect(await within(details).findByTestId("series-details-start-said-62")).toHaveTextContent("it waits for #51");
    expect(hoisted.start).toHaveBeenCalledWith("sr-1", "62");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(within(await open("56")).queryByTestId("series-details-start-56")).toBeNull();
  });

  it("Details_ShowTheMastersLastAct_WithItsReason", async () => {
    const detail = overviewDetail();
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "56");
    detail.tickets[at] = { ...detail.tickets[at],
      lastAct: { text: "merged #56 (now)", reason: "CI is green", at: new Date(Date.now() - 3 * 3600_000).toISOString() } };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);
    const details = await open("56");

    const last = within(details).getByTestId("series-details-lastact-56");
    expect(last).toHaveTextContent("Series master 3h ago: merged #56 (now)");
    expect(last).toHaveTextContent("— CI is green");
    expect(within(details).queryAllByRole("button")).toHaveLength(0);
  });

  it("Details_WithoutAnActOfTheMaster_ShowNoLastAct", async () => {
    render(<SeriesRunPage id="sr-1" />);

    expect(within(await open("56")).queryByTestId("series-details-lastact-56")).toBeNull();
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
