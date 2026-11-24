import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { overviewDetail, seriesDetail } from "./seriesFixtures";

// 2026-11-23-f562: the Overview — a node's menu says why it has its colour and every act in it is real: Merge posts the
// series' merge, Retry posts the ticket page's retry, every link names its route; a disabled Merge says why; Escape and a
// click outside close the menu; one act's outcome shows once; the chips, list and bar agree; a row lights its chain.

const hoisted = vi.hoisted(() => ({
  merge: vi.fn(), retry: vi.fn(), refresh: vi.fn(), fetch: vi.fn(), changes: vi.fn(), replace: vi.fn(), push: vi.fn(), lacks: false, search: "",
}));
vi.mock("@/lib/seriesRunsApi", () => ({
  mergeSeriesTicket: (...a: unknown[]) => hoisted.merge(...a),
  fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a),
  fetchSeriesRunChanges: (...a: unknown[]) => hoisted.changes(...a),
}));
vi.mock("@/lib/ticketsApi", () => ({
  retryAttempt: (...a: unknown[]) => hoisted.retry(...a),
  refreshTicketBase: (...a: unknown[]) => hoisted.refresh(...a),
}));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: hoisted.replace, push: hoisted.push }),
}));

import { SeriesRunPage } from "../SeriesRunPage";

beforeEach(() => {
  hoisted.merge.mockReset().mockResolvedValue({ kind: "said", text: "#50 was not merged: another run holds this ticket." });
  hoisted.retry.mockReset().mockResolvedValue({ kind: "requested" });
  hoisted.refresh.mockReset().mockResolvedValue({ kind: "started", runId: "rf-2" });
  hoisted.fetch.mockReset().mockResolvedValue(overviewDetail());
  hoisted.changes.mockReset().mockResolvedValue({ changes: [], unread: [] });
  hoisted.replace.mockReset();
  hoisted.push.mockReset();
  hoisted.lacks = false;
  hoisted.search = "";
});

const open = async (ticketId: string) => {
  fireEvent.click(await screen.findByTestId(`series-node-${ticketId}`));
  return screen.getByTestId(`series-menu-${ticketId}`);
};

describe("SeriesOverviewSection", () => {
  // 2026-11-23-0219: a ready merge whose required build has not started is listed with its badges and Merge when CI is green,
  // which posts the one merge; the draft badge shows on a draft.
  it("Overview_ABuildNotStarted_ListsMergeWhenGreen_WithItsBadges_AndMergePostsTheMerge", async () => {
    const detail = { ...overviewDetail(), mergeArms: true };
    const at = detail.tickets.findIndex((ticket) => ticket.ticketId === "50");
    detail.tickets[at] = { ...detail.tickets[at], hold: { ...detail.tickets[at].hold!, case: "ci_not_started", addressee: "nobody", act: null },
      pullRequests: detail.tickets[at].pullRequests.map((pr) => ({ ...pr, ci: "not_started", isDraft: true })) };
    detail.mergeOrder = detail.mergeOrder.map((step) => (step.ticketId === "50" ? { ...step, neededBy: [] } : step));
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);

    const row = await screen.findByTestId("series-row-50");
    expect(screen.getByTestId("series-list-when-green")).toBeTruthy();
    expect([...within(row).getByTestId("series-badges-50").querySelectorAll("[data-badge]")].map((b) => b.textContent))
      .toEqual(["Draft", "Build not started"]);
    fireEvent.click(within(row).getByTestId("series-row-merge-50"));
    await waitFor(() => expect(hoisted.merge).toHaveBeenCalledWith("sr-1", "50"));
    expect(within(row).getByTestId("series-row-merge-50").textContent).not.toBe("Merge");
  });

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

  it("Overview_ChipsListAndBar_Agree", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const chips = await screen.findByTestId("series-chips");

    expect(within(chips).getByTestId("series-chip-now")).toHaveTextContent("1 merge now");
    expect(within(chips).getByTestId("series-chip-needed")).toHaveTextContent("2 more merges needed for 1 ticket");
    expect(within(chips).getByTestId("series-chip-question")).toHaveTextContent("1 question");
    expect(within(chips).getByTestId("series-chip-failed")).toHaveTextContent("1 failed");
    expect(within(chips).getByTestId("series-chip-running")).toHaveTextContent("1 running");
    expect(screen.getAllByTestId(/^series-row-\d+$/).map((r) => r.dataset.kind)).toEqual(["now", "question", "failed", "needed", "needed"]);
    expect(screen.getByTestId("series-list-needed")).toHaveTextContent("Needed next · so #62 can start");
    expect(screen.getByTestId("series-list-quiet")).toHaveTextContent("3 more tickets");
    expect(screen.getByTestId("series-section-overview")).toHaveTextContent("3");
  });

  // 2026-11-23-8071: a viewer who may not merge — the merge now is not their move anywhere on the Overview.
  it("WithoutMergeRight_TheMergeNow_IsNotTheViewersMove", async () => {
    hoisted.lacks = true;
    render(<SeriesRunPage id="sr-1" />);
    const chips = await screen.findByTestId("series-chips");

    expect(within(chips).queryByTestId("series-chip-now")).toBeNull();
    expect(screen.getAllByTestId(/^series-row-\d+$/).map((r) => r.dataset.kind)).toEqual(["question", "failed", "now", "needed", "needed"]);
    expect(screen.getByTestId("series-section-overview")).toHaveTextContent("2");
  });

  it("Menu_MergeNow_PostsTheSeriesMerge_AndItsOutcomeShowsOnce", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const menu = await open("50");
    expect(within(menu).getByTestId("series-menu-say-50")).toHaveTextContent("next in the series' merge order");
    expect(within(menu).getByTestId("series-menu-pr-server-50")).toHaveAttribute("href", "https://git.test/server/pull/9937");
    expect(within(menu).getByTestId("series-menu-ticket-50")).toHaveAttribute("href", "/tickets/shop/50");

    fireEvent.click(within(menu).getByTestId("series-menu-merge-50"));

    await waitFor(() => expect(hoisted.merge).toHaveBeenCalledWith("sr-1", "50"));
    expect(await screen.findAllByTestId("series-merge-said")).toHaveLength(1);
    expect(screen.getByTestId("series-act-outcome")).toHaveTextContent(/^#50 was not merged: another run holds this ticket\.$/);
    expect(screen.getByTestId("series-node-50")).toHaveFocus(); // the act closed the menu and gave focus back
    expect(hoisted.push).not.toHaveBeenCalled();
  });

  it("Row_MergeNow_PostsTheSameMerge", async () => {
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.click(await screen.findByTestId("series-row-merge-50"));

    await waitFor(() => expect(hoisted.merge).toHaveBeenCalledWith("sr-1", "50"));
  });

  it("Menu_NeededMerge_IsDisabledWithItsReason_AndOpensItsPullRequest", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const menu = await open("51");
    const merge = within(menu).getByTestId("series-menu-merge-51");

    expect(merge).toBeDisabled();
    expect(merge).toHaveAttribute("title", "Needed so #62 can start. Merges after #50. Nothing for you to do yet.");
    expect(within(menu).getByTestId("series-menu-pr-server-51")).toHaveAttribute("target", "_blank");
    expect(screen.getByTestId("series-row-merge-55")).toBeDisabled();
  });

  it("Menu_Failed_RetryPostsTheTicketPagesRetry_SeeWhyAndConversationNameTheirRoutes", async () => {
    render(<SeriesRunPage id="sr-1" />);
    const menu = await open("59");
    expect(within(menu).getByTestId("series-menu-why-59")).toHaveAttribute("href", "/jobs/run-59/why");
    expect(within(menu).getByTestId("series-menu-conversation-59")).toHaveAttribute("href", "/tickets/shop/59"); // the ticket's rework talk

    fireEvent.click(within(menu).getByTestId("series-menu-retry-59"));

    await waitFor(() => expect(hoisted.retry).toHaveBeenCalledWith("run-59"));
    expect(await screen.findByTestId("series-retry-outcome")).toHaveTextContent("Retry requested for #59");
    expect(hoisted.fetch).toHaveBeenCalledTimes(2); // read again after the act
  });

  // 2026-11-24-9dc0e: a refused refresh's row offers Try again, which posts the refresh of its pull request.
  it("Row_ARefusedRefresh_TryAgainPostsTheRefreshOfItsPullRequest", async () => {
    const detail = overviewDetail();
    const ticket = detail.tickets.find((x) => x.ticketId === "59")!;
    const url = "https://git.test/client/pull/77";
    ticket.status = "success";
    ticket.hold = { addressee: "decide", case: "refresh_refused", act: { kind: "pull_request", target: url }, since: null, attempt: 1,
      names: [], detail: null };
    hoisted.fetch.mockResolvedValue(detail);
    render(<SeriesRunPage id="sr-1" />);

    fireEvent.click(await screen.findByTestId("series-row-refresh-59"));

    await waitFor(() => expect(hoisted.refresh).toHaveBeenCalledWith("shop", "59", url));
    expect(await screen.findByTestId("series-retry-outcome")).toHaveTextContent("#59: The refresh started again.");
  });

  it("Retry_ARefusal_IsShownAsTheServerWroteIt", async () => {
    hoisted.retry.mockResolvedValue({ kind: "refused", refusal: { text: "Attempt 1 is not finished." } });
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.click(await screen.findByTestId("series-row-retry-59"));

    expect(await screen.findByTestId("series-retry-outcome")).toHaveTextContent("#59: Attempt 1 is not finished.");
  });

  it("Menu_MergedAndPrWaiting_OpenTheirPullRequests", async () => {
    render(<SeriesRunPage id="sr-1" />);
    expect(within(await open("48")).getByTestId("series-menu-pr-server-48")).toHaveAttribute("href", "https://git.test/server/pull/9930");
    const waiting = await open("56");
    expect(within(waiting).getByTestId("series-menu-pr-client-56")).toHaveAttribute("href", "https://git.test/client/pull/9980");
    expect(within(waiting).getByTestId("series-menu-merge-56")).toBeDisabled();
    expect(within(waiting).getByTestId("series-menu-ticket-56")).toHaveAttribute("href", "/tickets/shop/56");
  });

  it("CourseSelect_LightsTheChosenTicketsCourse_AndAllLightsEverything", async () => {
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.change(await screen.findByTestId("series-graph-from"), { target: { value: "61" } });

    expect(screen.getByTestId("series-node-56")).toHaveClass("dim");
    expect(screen.getByTestId("series-node-48")).not.toHaveClass("dim");
    fireEvent.change(screen.getByTestId("series-graph-from"), { target: { value: "" } });
    expect(screen.getByTestId("series-node-56")).not.toHaveClass("dim");
  });

  it("Overview_NothingToDo_SaysSoOnce", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail());
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findAllByText("Nothing for you to do now.")).toHaveLength(1);
    expect(screen.queryByTestId("series-chips")).toBeNull();
  });

  it("Menu_QuestionRunningWaiting_LinkToTheirRoutes", async () => {
    render(<SeriesRunPage id="sr-1" />);
    expect(within(await open("61")).getByTestId("series-menu-answer-61")).toHaveAttribute("href", "/series/sr-1?section=talk");
    expect(within(await open("63")).getByTestId("series-menu-run-63")).toHaveAttribute("href", "/jobs/run-63");
    expect(within(await open("62")).getByTestId("series-menu-conversation-62")).toHaveAttribute("href", "/series/sr-1?section=talk");
  });

  it("Menu_ShowChain_LightsTheCourse_AndClosesTheMenu", async () => {
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.click(within(await open("56")).getByTestId("series-menu-chain-56"));

    expect(screen.queryByTestId("series-menu-56")).toBeNull();
    expect(screen.getByTestId("series-graph-from")).toHaveValue("56");
    expect(screen.getByTestId("series-node-61")).toHaveClass("dim");
    expect(screen.getByTestId("series-node-55")).not.toHaveClass("dim");
  });

  it("Menu_ClosesOnEscape_AndOnAClickOutside", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await open("50");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByTestId("series-menu-50")).toBeNull();
    expect(screen.getByTestId("series-node-50")).toHaveFocus();

    await open("50");
    fireEvent.keyDown(document, { key: "Escape" }); // focus is on the node: it closes
    expect(screen.queryByTestId("series-menu-50")).toBeNull();
    await open("50");
    fireEvent.mouseDown(screen.getByTestId("series-chips"));
    expect(screen.queryByTestId("series-menu-50")).toBeNull();
  });

  it("Row_Hover_LightsItsChain", async () => {
    render(<SeriesRunPage id="sr-1" />);
    fireEvent.mouseEnter(await screen.findByTestId("series-row-61"));

    expect(screen.getByTestId("series-node-59")).toHaveClass("dim");
    expect(screen.getByTestId("series-node-50")).not.toHaveClass("dim");
    fireEvent.mouseLeave(screen.getByTestId("series-row-61"));
    expect(screen.getByTestId("series-node-59")).not.toHaveClass("dim");
  });

  it("Overview_WithoutRunsControl_ShowsNoMergeAndNoRetry", async () => {
    hoisted.lacks = true;
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-chips");

    expect(screen.queryAllByRole("button", { name: /^Merge/ })).toHaveLength(0);
    expect(screen.queryByTestId("series-row-retry-59")).toBeNull();
    expect(screen.getByTestId("series-row-pr-server-50")).toHaveAttribute("href", "https://git.test/server/pull/9937");
  });
});
