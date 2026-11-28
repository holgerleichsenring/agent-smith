import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SeriesStatusLine } from "../SeriesStatusLine";
import { seriesDetail } from "./seriesFixtures";

// 2026-11-25-433fa: the chat's status line words what the server read — merged of total, the tickets at work (three named,
// the rest counted), who waits on a person, the next merge and how far — and derives nothing.

describe("SeriesStatusLine", () => {
  it("SeriesStatusLine_ARunningSeries_WordsTheServersFields", () => {
    render(<SeriesStatusLine detail={seriesDetail({
      counts: { total: 25, landed: 9, waiting: 2, held: 1, notStarted: 10 },
      workingOn: ["#4", "#7", "#9", "#11"], nextMerge: "#12", progress: "third",
    })} />);

    expect(screen.getByTestId("series-status-line")).toHaveTextContent(
      "9 of 25 merged · 4 in progress: #4, #7, #9 +1 · 1 needs you · next: #12 · about a third done");
  });

  it("SeriesStatusLine_AFinishedSeries_SaysSoAndNoProgressWord", () => {
    render(<SeriesStatusLine detail={seriesDetail({
      state: "done", counts: { total: 3, landed: 3, waiting: 0, held: 0, notStarted: 0 }, progress: "all",
    })} />);

    expect(screen.getByTestId("series-status-line")).toHaveTextContent(/^done · 3 of 3 merged$/);
  });

  // 2026-11-28-d1ffc: seen live as "merging into the default branch · 4 of 4 merged" while the final pull request waited.
  it("SeriesStatusLine_ADeliveringSeries_SaysTheFinalPullRequestWaitsForReview_AndLinksIt", () => {
    render(<SeriesStatusLine detail={seriesDetail({
      state: "delivering", integrationBranch: "agent-smith/series/sr-1", review: "end",
      counts: { total: 4, landed: 4, waiting: 0, held: 0, notStarted: 0 }, progress: "all",
      deliveries: [{ repo: "server", url: "https://git.test/server/pull/90", number: "90", said: null }],
    })} />);

    const line = screen.getByTestId("series-status-line");
    expect(line).toHaveTextContent(
      "final pull request waits for your review (1 repo) · 4 of 4 merged into the series branch server #90");
    expect(line).not.toHaveTextContent("merging");
    expect(screen.getByTestId("series-final-link-server")).toHaveAttribute("href", "https://git.test/server/pull/90");
  });
});
