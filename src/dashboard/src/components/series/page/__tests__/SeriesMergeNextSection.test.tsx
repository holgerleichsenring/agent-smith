import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SeriesRunMergeStep, SeriesRunPullRequest } from "@/types/seriesRuns";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dce: Merge next — rows in merge order with each repo's pull request, its CI and link; only the ready row's
// button is enabled, the others say what comes first; a merge's outcome or refusal stays on the row and the series is
// read again.

const hoisted = vi.hoisted(() => ({ merge: vi.fn(), lacks: false }));
vi.mock("@/lib/seriesRunsApi", () => ({ mergeSeriesTicket: (...a: unknown[]) => hoisted.merge(...a) }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));

import { SeriesMergeNextSection } from "../SeriesMergeNextSection";

const pr = (over: Partial<SeriesRunPullRequest> = {}): SeriesRunPullRequest => ({
  repo: "server", number: "9935", url: "https://git/server/pull/9935", state: "open", ci: "green", ciFailure: null, ciLog: null,
  target: null, ...over,
});

const step = (ticketId: string, wait: SeriesRunMergeStep["wait"], after: string[] = []): SeriesRunMergeStep =>
  ({ ticketId, key: `#${ticketId}`, depth: 0, wait, after });

const detail = () => seriesDetail({
  tickets: [
    seriesTicket({ ticketId: "49", key: "#49", title: "Domain", pullRequests: [
      pr(), pr({ repo: "worker", number: "12", ci: "red", ciFailure: "build: error CS1002", ciLog: "https://ci/log/7" }),
      pr({ repo: "client", number: "3", state: "closed" })] }),
    seriesTicket({ ticketId: "50", key: "#50", title: "Lifecycle", pullRequests: [pr({ number: "9940" })] }),
    seriesTicket({ ticketId: "51", key: "#51", title: "Callbacks", pullRequests: [pr({ number: "9941" })] }),
  ],
  mergeOrder: [step("49", "ready"), step("50", "after", ["#49"]), step("51", "retarget")],
});

beforeEach(() => {
  hoisted.merge.mockReset();
  hoisted.lacks = false;
});

describe("SeriesMergeNextSection", () => {
  it("MergeNext_RowsInMergeOrder_CarryEachOpenPullRequestWithItsCi", () => {
    render(<SeriesMergeNextSection detail={detail()} reload={() => {}} />);

    const rows = [...screen.getByTestId("series-merge-order").querySelectorAll("li")].map((li) => li.dataset.wait);
    expect(rows).toEqual(["ready", "after", "retarget"]);
    const worker = screen.getByTestId("series-merge-pr-49-worker");
    expect(worker).toHaveTextContent("PR #12");
    expect(worker).toHaveTextContent("CI red");
    expect(worker).toHaveTextContent("build: error CS1002");
    expect(worker.querySelector("a[href='https://ci/log/7']")).not.toBeNull();
    expect(screen.getByTestId("series-merge-pr-49-server")).toHaveTextContent("CI green");
    expect(screen.queryByTestId("series-merge-pr-49-client")).toBeNull();
  });

  it("MergeNext_OnlyTheReadyRowsButton_IsEnabled_AndTheOthersSayWhy", () => {
    render(<SeriesMergeNextSection detail={detail()} reload={() => {}} />);

    expect(screen.getByTestId("series-merge-button-49")).toBeEnabled();
    expect(screen.getByTestId("series-merge-button-50")).toBeDisabled();
    expect(screen.getByTestId("series-merge-button-51")).toBeDisabled();
    expect(screen.getByTestId("series-merge-why-50")).toHaveTextContent("Merge #49 first.");
    expect(screen.getByTestId("series-merge-why-51")).toHaveTextContent("moved onto the default branch");
  });

  it("MergeNext_Merge_ShowsWhatTheRunDid_AndReadsTheSeriesAgain", async () => {
    const reload = vi.fn();
    hoisted.merge.mockResolvedValue({ kind: "merged", answer: { ticketId: "49", pullRequests: [
      { repo: "server", outcome: "merged", text: "server: merged (https://git/server/pull/9935)" }] } });
    render(<SeriesMergeNextSection detail={detail()} reload={reload} />);

    fireEvent.click(screen.getByTestId("series-merge-button-49"));

    expect(await screen.findByTestId("series-merge-outcome")).toHaveTextContent("merged server");
    expect(hoisted.merge).toHaveBeenCalledWith("sr-1", "49");
    expect(reload).toHaveBeenCalled();
  });

  it("MergeNext_ARefusal_ShowsTheServersSentenceOnTheRow", async () => {
    hoisted.merge.mockResolvedValue({ kind: "said", text: "#49 was not merged: another run holds this ticket." });
    render(<SeriesMergeNextSection detail={detail()} reload={() => {}} />);

    fireEvent.click(screen.getByTestId("series-merge-button-49"));

    await waitFor(() => expect(screen.getByTestId("series-merge-49")).toHaveTextContent("another run holds this ticket"));
  });

  it("MergeNext_ACallerWithoutRunsControl_SeesNoButton", () => {
    hoisted.lacks = true;
    render(<SeriesMergeNextSection detail={detail()} reload={() => {}} />);

    expect(screen.queryByTestId("series-merge-button-49")).toBeNull();
  });

  it("MergeNext_NothingWaiting_SaysSo", () => {
    render(<SeriesMergeNextSection detail={seriesDetail()} reload={() => {}} />);

    expect(screen.getByTestId("series-merge-empty")).toHaveTextContent("No pull request is waiting to be merged.");
  });
});
