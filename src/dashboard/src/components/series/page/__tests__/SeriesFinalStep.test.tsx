import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { SeriesFinalStep } from "../SeriesFinalStep";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-24-f9c5f: the series branch on the page — the final step
// into the default branch with what it waits for and each repo's pull request.

const Branch = "agent-smith/series/sr-1";

beforeEach(() => resetLanguage());

describe("SeriesFinalStep", () => {
  it("SeriesFinalStep_WithoutABranch_IsNotShown", () => {
    render(<SeriesFinalStep detail={seriesDetail()} />);
    expect(screen.queryByTestId("series-final-step")).toBeNull();
  });

  it("SeriesFinalStep_TicketsLeft_SaysWhatItWaitsFor", () => {
    render(<SeriesFinalStep detail={seriesDetail({ integrationBranch: Branch })} />);
    expect(screen.getByTestId("series-list-final")).toHaveTextContent(`Then: ${Branch} into the default branch`);
    expect(screen.getByTestId("series-final-waits")).toHaveTextContent(`Waits for 2 tickets to land on ${Branch}.`);
  });

  it("SeriesFinalStep_Delivering_ShowsEachRepoWithItsSentence_AndAPersonsReviewUnderEnd", () => {
    render(<SeriesFinalStep detail={seriesDetail({
      integrationBranch: Branch, state: "delivering", review: "end",
      tickets: [seriesTicket({ landed: true })],
      deliveries: [{ repo: "server", url: "https://git.test/server/pull/90", number: "90", said: "waits for your merge into the default branch." }],
    })} />);
    expect(screen.getByTestId("series-final-waits")).toHaveTextContent("these wait for your review and merge (review: end)");
    expect(screen.getByTestId("series-final-server")).toHaveTextContent("server #90 waits for your merge");
    expect(screen.getByRole("link", { name: "#90" })).toHaveAttribute("href", "https://git.test/server/pull/90");
  });
});
