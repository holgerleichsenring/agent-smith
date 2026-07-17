import { render, screen, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import PullRequestsPage from "../page";
import { fetchPullRequests } from "@/lib/pullRequestsApi";
import type { PullRequest } from "@/types/hub-events";

// p0347: the Pull Requests page — opened PRs are the headline (metric strip +
// .rrow rows with a green status pill + external link + a link back to the run);
// no_changes/failed attempts are an honest MUTED section below, never hidden.

vi.mock("@/lib/pullRequestsApi", () => ({ fetchPullRequests: vi.fn() }));
const mockFetch = vi.mocked(fetchPullRequests);

function pr(over: Partial<PullRequest> = {}): PullRequest {
  return {
    runId: "r1",
    ticketId: "4711",
    ticketTitle: "Fix the login bug",
    pipeline: "fix-bug",
    repo: "server",
    status: "opened",
    url: "https://git/pr/1",
    reason: null,
    openedAt: new Date().toISOString(),
    ...over,
  };
}

beforeEach(() => mockFetch.mockReset());

describe("PullRequestsPage", () => {
  it("PullRequestsPage_OpenedHeadline_AttemptsMutedBelow", async () => {
    mockFetch.mockResolvedValue([
      pr({ runId: "r1", repo: "server", status: "opened", url: "https://git/pr/1" }),
      pr({ runId: "r2", repo: "web", status: "no_changes", url: null, reason: "nothing to commit" }),
      pr({ runId: "r3", repo: "api", status: "failed", url: null, reason: "auth rejected the push" }),
    ]);
    render(<PullRequestsPage />);

    const opened = await screen.findByTestId("pr-opened-section");
    expect(within(opened).getByTestId("pr-opened-count")).toHaveTextContent("1");
    // The opened row links OUT (new tab) and BACK to its run.
    const link = within(opened).getByTestId("pr-row-r1-server-link");
    expect(link).toHaveAttribute("href", "https://git/pr/1");
    expect(link).toHaveAttribute("target", "_blank");
    expect(within(opened).getByTestId("pr-row-r1-server-run")).toHaveAttribute("href", "/jobs/r1");
    expect(within(opened).getByTestId("pr-row-r1-server-status")).toHaveTextContent("opened");

    // The muted attempts section sits BELOW the headline and names why.
    const attempts = screen.getByTestId("pr-attempts-section");
    expect(within(attempts).getByTestId("pr-attempts-count")).toHaveTextContent("2");
    expect(within(attempts).getByTestId("pr-attempt-r2-web-status")).toHaveTextContent("no changes");
    expect(within(attempts).getByTestId("pr-attempt-r3-api-status")).toHaveTextContent("failed");
    expect(attempts).toHaveTextContent("nothing to commit");
    expect(attempts).toHaveTextContent("auth rejected the push");
    // Opened headline comes before the muted attempts section in the DOM.
    expect(opened.compareDocumentPosition(attempts)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it("PullRequestsPage_MultiRepoRun_ShowsEveryRepoRow", async () => {
    // One run opened PRs in two repos — the page keeps a row per repo.
    mockFetch.mockResolvedValue([
      pr({ runId: "r9", repo: "server", url: "https://git/pr/10" }),
      pr({ runId: "r9", repo: "web", url: "https://git/pr/11" }),
    ]);
    render(<PullRequestsPage />);

    await screen.findByTestId("pr-opened-section");
    expect(screen.getByTestId("pr-metric-total")).toHaveTextContent("2");
    expect(screen.getByTestId("pr-row-r9-server-link")).toHaveAttribute("href", "https://git/pr/10");
    expect(screen.getByTestId("pr-row-r9-web-link")).toHaveAttribute("href", "https://git/pr/11");
  });

  it("PullRequestsPage_NoPrs_HonestEmptyState", async () => {
    mockFetch.mockResolvedValue([]);
    render(<PullRequestsPage />);
    expect(await screen.findByTestId("pr-empty")).toHaveTextContent("No pull requests yet");
    expect(screen.queryByTestId("pr-opened-section")).not.toBeInTheDocument();
    expect(screen.queryByTestId("pr-attempts-section")).not.toBeInTheDocument();
    expect(screen.getByTestId("pr-metric-total")).toHaveTextContent("0");
  });
});
