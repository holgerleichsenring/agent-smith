import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SeriesSectionBar } from "../SeriesSectionBar";
import { seriesDetail } from "./seriesFixtures";

// 2026-11-23-31dcd: the series bar — back to the list, name, state with the question, and the facts at its foot.
// 2026-11-25-9992j: no progress bar, counts or sections.

describe("SeriesSectionBar", () => {
  it("SeriesSectionBar_RendersNameStateAndFacts_NoProgressOrSections", () => {
    render(<SeriesSectionBar detail={seriesDetail({ question: "Keep it?" })} />);

    expect(screen.getByTestId("series-back")).toHaveAttribute("href", "/series");
    expect(screen.getByTestId("series-head")).toHaveTextContent("series · 3 tickets");
    expect(screen.getByTestId("series-head")).toHaveTextContent("Approval workflow");
    expect(screen.getByTestId("series-state-word")).toHaveTextContent("running · 1 needs you");
    expect(screen.getByTestId("series-repos")).toHaveTextContent("server · worker");
    expect(screen.getByTestId("series-conversation-link")).toHaveAttribute("href", "/spec-dialog?open=conv-1&on=d-thread1");
    expect(screen.getByTestId("series-facts")).toHaveTextContent("$41.20 so far");
    expect(screen.queryByTestId("series-progress")).toBeNull();
    expect(screen.queryByTestId("series-counts")).toBeNull();
    expect(screen.queryByRole("navigation")).toBeNull();
  });

  // 2026-11-28-d1ffc: the rail says the final pull request waits for review and links it.
  it("SeriesSectionBar_ADeliveringSeries_LinksTheFinalPullRequest", () => {
    render(<SeriesSectionBar detail={seriesDetail({
      state: "delivering", integrationBranch: "agent-smith/series/sr-1",
      deliveries: [{ repo: "server", url: "https://git.test/server/pull/90", number: "90", said: null }],
    })} />);

    expect(screen.getByTestId("series-state-word")).toHaveTextContent("final pull request waits for your review (1 repo) server #90");
    expect(screen.getByTestId("series-rail-final-link-server")).toHaveAttribute("href", "https://git.test/server/pull/90");
  });
});
