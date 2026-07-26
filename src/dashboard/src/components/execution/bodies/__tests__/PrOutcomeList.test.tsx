import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { PrOutcomeList } from "../PrOutcomeList";
import { EventType, type PullRequestOutcomeEvent } from "@/types/hub-events";

function outcome(over: Partial<PullRequestOutcomeEvent>): PullRequestOutcomeEvent {
  return {
    type: EventType.PullRequestOutcome,
    runId: "r",
    timestamp: "2026-06-04T10:00:00.000Z",
    repo: "server",
    status: "opened",
    url: "https://github.com/test/repo/pull/42",
    reason: null,
    ...over,
  };
}

describe("PrOutcomeList", () => {
  it("PrStep_RepoWithNoChanges_ShowsNoPrNeeded_NotRedExit1", () => {
    render(<PrOutcomeList events={[outcome({ repo: "docs", status: "no_changes", url: null })]} />);
    const row = screen.getByTestId("pr-outcome-docs");
    expect(row).toHaveTextContent("no changes — no PR needed");
    expect(row).not.toHaveTextContent("exit 1");
    expect(row).not.toHaveTextContent("failed");
    expect(row.getAttribute("data-status")).toBe("no_changes");
  });

  it("PrStep_CreatedPr_RendersAsClickableLink", () => {
    render(<PrOutcomeList events={[outcome({ repo: "server", status: "opened" })]} />);
    const link = screen.getByTestId("pr-outcome-server-link");
    expect(link.tagName).toBe("A");
    expect(link).toHaveAttribute("href", "https://github.com/test/repo/pull/42");
  });

  it("PrStep_FailedWithSurvivingDraftPr_UrlStaysClickable", () => {
    // p0372: a failed outcome that still carries a PR (draft survived the
    // failure) renders it as the clickable draft PrButton, not dead text.
    render(
      <PrOutcomeList
        events={[outcome({ repo: "api", status: "failed", url: "https://az/api/pr/9", reason: "keystone gate failed" })]}
      />,
    );
    const link = screen.getByTestId("pr-outcome-api-link");
    expect(link.tagName).toBe("A");
    expect(link).toHaveAttribute("href", "https://az/api/pr/9");
    expect(link).toHaveTextContent("Draft pull request");
  });

  it("PrStep_FailedWithUrlInReason_UrlBecomesButton", () => {
    render(
      <PrOutcomeList
        events={[outcome({ repo: "web", status: "failed", url: null, reason: "push rejected — draft kept at https://az/web/pr/4" })]}
      />,
    );
    const link = screen.getByTestId("pr-outcome-web-link");
    expect(link).toHaveAttribute("href", "https://az/web/pr/4");
    const row = screen.getByTestId("pr-outcome-web");
    expect(row).toHaveTextContent("failed");
    // The raw URL left the reason prose — the button carries it now.
    expect(row.textContent).not.toContain("https://");
  });

  it("Step_GenuineFailure_ShowsRealReason_NotHiddenStdout", () => {
    render(
      <PrOutcomeList
        events={[outcome({ repo: "api", status: "failed", url: null, reason: "remote rejected: protected branch" })]}
      />,
    );
    const row = screen.getByTestId("pr-outcome-api");
    expect(row).toHaveTextContent("failed");
    expect(row).toHaveTextContent("remote rejected: protected branch");
  });
});
