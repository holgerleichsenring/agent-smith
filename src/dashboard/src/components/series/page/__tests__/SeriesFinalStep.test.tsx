import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { SeriesFinalStep } from "../SeriesFinalStep";
import { SeriesTicketsSection } from "../SeriesTicketsSection";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-24-f9c5f: the series branch on the page — each pull request's target in the tickets table, and the final step
// into the default branch with what it waits for and each repo's pull request.

const Branch = "agent-smith/series/sr-1";

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: () => {} }) }));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRunChanges: () => Promise.resolve({ changes: [], unread: [] }) }));

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

  it("SeriesFinalStep_Delivering_ShowsEachRepoWithItsSentence_AndAPersonsMergeUnderManual", () => {
    render(<SeriesFinalStep detail={seriesDetail({
      integrationBranch: Branch, state: "delivering", mergeWhen: "manual",
      tickets: [seriesTicket({ landed: true })],
      deliveries: [{ repo: "server", url: "https://git.test/server/pull/90", number: "90", said: "waits for your merge into the default branch." }],
    })} />);
    expect(screen.getByTestId("series-final-waits")).toHaveTextContent("these wait for your merge (merge.when: manual)");
    expect(screen.getByTestId("series-final-server")).toHaveTextContent("server #90 waits for your merge");
    expect(screen.getByRole("link", { name: "#90" })).toHaveAttribute("href", "https://git.test/server/pull/90");
  });

  it("SeriesTicketsSection_NamesEachPullRequestsTarget_TheSeriesBranchAsSuch", () => {
    render(<SeriesTicketsSection detail={seriesDetail({
      integrationBranch: Branch,
      tickets: [seriesTicket({ ticketId: "49", pullRequests: [{ repo: "server", number: "1", url: "u", state: "open", ci: null, ciFailure: null,
        ciLog: null, target: Branch }] }), seriesTicket({ ticketId: "50" })],
    })} />);
    expect(screen.getByTestId("series-ticket-into-49")).toHaveTextContent("series branch");
    expect(screen.getByTestId("series-ticket-into-50")).toHaveTextContent("–");
  });
});
