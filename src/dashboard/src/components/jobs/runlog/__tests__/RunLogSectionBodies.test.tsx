import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";

// 2026-11-17-6d9eb: a section's body, plain — the ticket with its tracker link, the plan and ledger, the criteria with
// their overrule, the pull requests with the run report.

vi.mock("@/hooks/useSpecMarkdown", () => ({ useSpecMarkdown: () => ({ content: "# Spec\nmake it so", loading: false }) }));
vi.mock("@/hooks/usePlanMarkdown", () => ({ usePlanMarkdown: () => ({ content: null, loading: false }) }));
vi.mock("@/hooks/useResultMarkdown", () => ({ useResultMarkdown: () => ({ content: "# Result\nall good", loading: false, error: null }) }));
vi.mock("@/hooks/useRunDecisions", () => ({
  useRunDecisions: () => [{ stepIndex: 2, name: "Use the outbox", reason: "at-least-once", category: "Architecture", recordedAt: "2026-10-13T06:10:00Z" }],
}));

const { CheckBody, DeliveredBody, PlanBody, TicketBody, ledgerCount } = await import("../RunLogSectionBodies");

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "success", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:00:00Z", finishedAt: "2026-10-13T07:00:00Z", sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 4,
    lastEventType: null, costUsd: 0, llmCalls: 0, ticketId: "13", ticketTitle: "Upgrade", agentName: null, cancelRequested: false, ...over,
  };
}

const fetched = {
  runId: "r1", type: EventType.TicketFetched, timestamp: "2026-10-13T06:00:01Z", ticketId: "13", title: "Upgrade",
  description: "Upgrade **every** dependency.", state: "Active", labels: ["agent"], attachmentCount: 2, source: "GitHub",
  webUrl: "https://example.test/issues/13",
} as unknown as RunEvent;

describe("RunLogSectionBodies", () => {
  it("TicketBody_SaysTheTicketAndLinksTheTrackerAndThePage", () => {
    render(<TicketBody snapshot={snap()} events={[fetched]} ticket={{ label: "GH-13", href: "/tickets/shop/13", project: "shop", number: 1, of: 1, attempt: null, newest: true }} />);
    expect(screen.getByTestId("ticket-body-title")).toHaveTextContent("13 · Upgrade");
    expect(screen.getByTestId("ticket-body-facts")).toHaveTextContent("Active · GitHub · labels agent · 2 attachments");
    expect(screen.getByTestId("ticket-body-tracker")).toHaveAttribute("href", "https://example.test/issues/13");
    expect(screen.getByTestId("ticket-body-page")).toHaveAttribute("href", "/tickets/shop/13");
    expect(screen.queryByTestId("ticket-body-empty")).toBeNull();
  });

  // 2026-11-18-e06ba: the title line, the acceptance numbered, and one quiet line linking the ticket for its body.
  it("TicketBody_WithoutTheEvent_TitleNumberedAcceptanceAndOneQuietLine", () => {
    const acceptance = { criteria: [
      { text: "Every package is current", status: "met" as const, reason: null, citation: null },
      { text: "The build passes", status: "met" as const, reason: null, citation: null },
    ], outcome: null, ratifiedBy: null };
    render(<TicketBody snapshot={snap({ acceptance })} events={[]} ticket={{ label: "GH-13", href: "/tickets/shop/13", project: "shop", number: 1, of: 1, attempt: null, newest: true }} />);
    expect(screen.getByTestId("ticket-body-title")).toHaveTextContent("13 · Upgrade");
    expect(screen.getAllByTestId("ticket-body-criterion").map((li) => li.textContent)).toEqual(["1.Every package is current", "2.The build passes"]);
    expect(screen.getByTestId("ticket-body-empty")).toHaveTextContent("body not recorded on this run — open the ticket");
    expect(screen.getByTestId("ticket-body-page")).toHaveAttribute("href", "/tickets/shop/13");
    expect(screen.getByTestId("ticket-body").textContent).not.toContain("event stream");
  });

  it("PlanBody_TheSpecTheLedgerAndTheRunDecisions", () => {
    render(<PlanBody runId="r1" hasSpecs={false} events={[]} ledger={[
      { id: "a", activity: "Bump the server packages", status: "done", target: "Shop.Server/Shop.Persistence/Shop.Persistence.csproj" },
      { id: "b", activity: "Fix the build", status: "in_progress", target: null },
    ]} />);
    expect(screen.getByTestId("plan-body-spec")).toHaveTextContent("the work spec");
    expect(screen.getByTestId("plan-body-no-plan")).toHaveTextContent("No plan recorded yet");
    expect(screen.getByTestId("ledger-line-a")).toHaveTextContent("doneBump the server packagesShop.Server/…/Shop.Persistence.csproj");
    expect(screen.getByTestId("ledger-target-a")).toHaveAttribute("title", "Shop.Server/Shop.Persistence/Shop.Persistence.csproj");
    expect(screen.getByTestId("plan-body-ledger-count")).toHaveTextContent("1 done · 1 now · 0 to go");
    expect(screen.getByTestId("plan-body-decisions")).toHaveTextContent("Use the outbox — at-least-once");
    expect(ledgerCount([])).toBe("0 done · 0 now · 0 to go");
  });

  it("CheckBody_AnOverrule_IsRecorded_AndWithdrawn", () => {
    const record = vi.fn();
    const withdraw = vi.fn();
    const acceptance = { criteria: [{ text: "Every dependency is upgraded", status: "unmet" as const, reason: "client lags", citation: null }], outcome: null, ratifiedBy: null };
    const judged = { judgements: [], busy: false, error: null, record, withdraw };
    const { rerender } = render(<CheckBody snapshot={snap({ acceptance })} events={[]} judged={judged} />);
    fireEvent.click(screen.getByTestId("criterion-judgement-open"));
    fireEvent.change(screen.getByTestId("criterion-judgement-reason"), { target: { value: "the client was pinned on purpose" } });
    fireEvent.click(screen.getByTestId("criterion-judgement-save"));
    expect(record).toHaveBeenCalledWith("Every dependency is upgraded", "unmet", "met", "the client was pinned on purpose");
    rerender(<CheckBody snapshot={snap({ acceptance })} events={[]} judged={{ ...judged,
      judgements: [{ criterion: "Every dependency is upgraded", machineStatus: "unmet", humanStatus: "met", reason: "pinned", author: "op", recordedAt: "2026-10-13T08:00:00Z" }] } as never} />);
    fireEvent.click(screen.getByTestId("criterion-judgement-withdraw"));
    expect(withdraw).toHaveBeenCalledWith("Every dependency is upgraded");
  });

  // 2026-11-18-e06bc: a long citation collapses to "n files ›"; the section's layout stays.
  it("CheckBody_ALongCitation_Collapses", () => {
    const acceptance = { criteria: [{ text: "Every project is current", status: "met" as const, reason: "bumped",
      citation: "shop/src/A/A.csproj; shop/src/B/B.csproj; shop/src/C/C.csproj" }], outcome: null, ratifiedBy: null };
    render(<CheckBody snapshot={snap({ acceptance })} events={[]} judged={{ judgements: [], busy: false, error: null, record: vi.fn(), withdraw: vi.fn() } as never} />);
    expect(screen.getByTestId("verify-criterion-citation")).toHaveTextContent("cited: 3 files ›");
    fireEvent.click(screen.getByTestId("cited-toggle"));
    expect(screen.getAllByTestId("cited-item")).toHaveLength(3);
  });

  it("DeliveredBody_PullRequestsWithTheirStatus_AndTheRunReport", () => {
    render(<DeliveredBody runId="r1" snapshot={snap({ pullRequests: [{ repo: "server", url: "https://example.test/server/pull/1", status: "completed", isDraft: false }] })} />);
    expect(screen.getByTestId("delivered-pr")).toHaveTextContent("server · merged · pull request ↗");
    expect(screen.getByTestId("delivered-pr").querySelector("a")).toHaveAttribute("href", "https://example.test/server/pull/1");
    expect(screen.getByTestId("delivered-report")).toHaveTextContent("the run report");
  });

  it("DeliveredBody_AnOldRunWithOnlyPrUrl_StillLinksIt", () => {
    render(<DeliveredBody runId="r1" snapshot={snap({ prUrl: "https://example.test/server/pull/2" })} />);
    expect(screen.getByTestId("delivered-pr").querySelector("a")).toHaveAttribute("href", "https://example.test/server/pull/2");
  });
});
