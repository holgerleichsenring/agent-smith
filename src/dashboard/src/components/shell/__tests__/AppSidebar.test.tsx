import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";
import type { TicketListView } from "@/types/tickets";
import { list } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-11-16-0440c: the flat sidebar — every destination, the counts from the same reads the
// pages render, the item the URL marks, collapsing to icons, and the Recent group.

const hoisted = vi.hoisted(() => ({
  pathname: "/",
  push: vi.fn(),
  overview: null as OverviewSnapshot | null,
  tickets: null as TicketListView | null,
  prs: vi.fn(),
}));
vi.mock("next/navigation", () => ({
  usePathname: () => hoisted.pathname,
  useRouter: () => ({ push: hoisted.push }),
}));
vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({ client: {}, connectionState: HubConnectionState.Connected, overview: hoisted.overview, systemActivity: null }),
}));
vi.mock("@/lib/tickets/TicketListProvider", () => ({ useTicketList: () => ({ list: hoisted.tickets }) }));
vi.mock("@/lib/pullRequestsApi", () => ({ fetchPullRequests: (...a: unknown[]) => hoisted.prs(...a) }));

import { AppSidebar } from "../AppSidebar";
import { TicketBucketFilterProvider } from "@/lib/TicketBucketFilter";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";

function snap(runId: string, status: string): RunSnapshot {
  return {
    runId, pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status,
    prUrl: null, summary: null, startedAt: "2026-07-17T10:00:00Z", finishedAt: null,
    sandboxes: 1, stepIndex: 2, stepName: null, totalSteps: 5, lastEventType: null,
    costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
  };
}

// This jsdom build ships no localStorage; a Map stands in for it.
function stubStorage(): void {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (k: string) => backing.get(k) ?? null,
      setItem: (k: string, v: string) => void backing.set(k, v),
      removeItem: (k: string) => void backing.delete(k),
      clear: () => backing.clear(),
    },
  });
}

function renderAt(pathname: string, search = "") {
  hoisted.pathname = pathname;
  window.history.replaceState(null, "", `${pathname}${search}`);
  return render(
    <RunBucketFilterProvider>
      <TicketBucketFilterProvider>
        <AppSidebar />
      </TicketBucketFilterProvider>
    </RunBucketFilterProvider>,
  );
}

beforeEach(() => {
  stubStorage();
  hoisted.push.mockReset();
  hoisted.overview = { active: [snap("r-1", "running"), snap("r-2", "queued")], recent: [snap("r-3", "success")], systemActivity: null };
  hoisted.tickets = list([], { needsYou: 3, working: 2, done: 7 });
  hoisted.prs.mockReset().mockResolvedValue([{ status: "opened" }, { status: "opened" }, { status: "merged" }]);
});

describe("AppSidebar", () => {
  it("Sidebar_EveryDestination_InTheApprovedOrder", () => {
    renderAt("/");
    const hrefs = [...screen.getByTestId("app-sidebar").querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    expect(hrefs).toEqual(["/spec-dialog", "/tickets?bucket=needs-you", "/tickets", "/", "/pull-requests", "/overview", "/delivery"]);
    expect(screen.getByTestId("sidebar-group-Insight")).toBeInTheDocument();
    expect(screen.queryByTestId("sidebar-group-Recent")).not.toBeInTheDocument();
  });

  it("Sidebar_Counts_ComeFromTheTicketListTheHubAndThePullRequests", async () => {
    renderAt("/");
    expect(screen.getByTestId("sidebar-your-move-count")).toHaveTextContent("3");
    expect(screen.getByTestId("sidebar-your-move-count").className).toContain("hot");
    expect(screen.getByTestId("sidebar-tickets-count")).toHaveTextContent("12");
    expect(screen.getByTestId("sidebar-runs-count")).toHaveTextContent("2");
    expect(await screen.findByTestId("sidebar-pull-requests-count")).toHaveTextContent("2");
  });

  it("Sidebar_UnknownCounts_ShowNoNumber", () => {
    hoisted.tickets = null;
    hoisted.overview = null;
    hoisted.prs.mockRejectedValue(new Error("down"));
    renderAt("/");
    expect(screen.queryByTestId("sidebar-your-move-count")).not.toBeInTheDocument();
    expect(screen.queryByTestId("sidebar-runs-count")).not.toBeInTheDocument();
  });

  it("Sidebar_YourMove_IsMarkedOnTheNeedsYouBucket_AndTicketsElsewhereUnderTickets", () => {
    const view = renderAt("/tickets", "?bucket=needs-you");
    expect(screen.getByTestId("sidebar-your-move")).toHaveAttribute("aria-current", "page");
    expect(screen.getByTestId("sidebar-tickets")).not.toHaveAttribute("aria-current");
    view.unmount();

    renderAt("/tickets/shop/101");
    expect(screen.getByTestId("sidebar-tickets")).toHaveAttribute("aria-current", "page");
    expect(screen.getByTestId("sidebar-your-move")).not.toHaveAttribute("aria-current");
  });

  it("Sidebar_RunsIsMarkedOnTheHomeAndARun_NewWorkOnWorkItOut", () => {
    const view = renderAt("/jobs/2026-08-17T21-30-46-a98c");
    expect(screen.getByTestId("sidebar-runs")).toHaveAttribute("aria-current", "page");
    view.unmount();
    renderAt("/spec-dialog/conversations");
    expect(screen.getByTestId("sidebar-new-work")).toHaveAttribute("aria-current", "page");
    expect(screen.getByTestId("sidebar-runs")).not.toHaveAttribute("aria-current");
  });

  it("Sidebar_YourMoveFromAnotherPage_NavigatesToTheNeedsYouBucket", () => {
    renderAt("/overview");
    fireEvent.click(screen.getByTestId("sidebar-your-move"));
    expect(hoisted.push).toHaveBeenCalledWith("/tickets?bucket=needs-you");
  });

  it("Sidebar_Collapse_ShowsIconsOnly_AndIsRemembered", () => {
    const view = renderAt("/");
    fireEvent.click(screen.getByTestId("sidebar-toggle"));
    expect(screen.getByTestId("app-sidebar")).toHaveAttribute("data-collapsed", "true");
    expect(screen.getByTestId("sidebar-tickets")).toHaveAttribute("aria-label", "Tickets");
    expect(screen.queryByTestId("sidebar-tickets-count")).not.toBeInTheDocument();
    expect(screen.getByTestId("sidebar-your-move-dot")).toBeInTheDocument();
    view.unmount();

    renderAt("/");
    expect(screen.getByTestId("app-sidebar")).toHaveAttribute("data-collapsed", "true");
  });

  // 2026-11-17-8d1ca: a ticket page has its own section bar, so the sidebar beside it shows icons only.
  it("Sidebar_OnATicketPage_ShowsIconsOnly_WithoutTheToggle", () => {
    renderAt("/tickets/shop/101");
    expect(screen.getByTestId("app-sidebar")).toHaveAttribute("data-collapsed", "true");
    expect(screen.queryByTestId("sidebar-toggle")).toBeNull();
  });

  it("Sidebar_Recent_ListsTheTicketAndRunPagesOpened_NewestFirst", async () => {
    const first = renderAt("/tickets/shop/GH-101");
    first.unmount();
    renderAt("/jobs/2026-08-17T21-30-46-a98c");

    await waitFor(() => expect(screen.getByTestId("sidebar-group-Recent")).toBeInTheDocument());
    const recent = [...screen.getByTestId("app-sidebar").querySelectorAll("a[data-testid^='sidebar-recent-']")];
    expect(recent.map((a) => a.textContent)).toEqual(["Run a98c", "GH-101 · shop"]);
    expect(recent[1]).toHaveAttribute("href", "/tickets/shop/GH-101");
  });

  it("Sidebar_NoSettingsItems_TheyLiveBehindTheProfile", async () => {
    renderAt("/");
    const sidebar = screen.getByTestId("app-sidebar");
    expect(sidebar.querySelector("a[href^='/config']")).toBeNull();
    expect(sidebar.querySelector("a[href^='/system']")).toBeNull();
    await act(async () => fireEvent.click(screen.getByTestId("profile-button")));
    expect(screen.getByTestId("profile-settings")).toHaveAttribute("href", "/config");
  });
});
