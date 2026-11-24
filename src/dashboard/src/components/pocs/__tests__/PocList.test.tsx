import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { PocLifetime, PocList as PocListAnswer, PocRow } from "@/lib/pocsApi";
import { PocList } from "../PocList";

// 2026-11-23-933da: the PoCs list — the empty state, the target strip with its settings link for config readers, the
// cards and the state pills that narrow them.

const TARGETS = [{ name: "main", sourceRepo: "github/poc-source", root: "pocs" }];

function poc(id: number, name: string, state: string, lastRunId: string | null = null, latestRevision: number | null = null): PocRow {
  return {
    id, name, project: "poc-ann", target: "main", host: `${name}.poc.example.internal`, state, ownerName: "Ann", lastRunId,
    latestRevision,
  };
}

function list(pocs: PocRow[], mayImport = true): PocListAnswer {
  return { pocs, mayImport, targets: TARGETS };
}

describe("PocList", () => {
  it("PocList_NoPocs_ShowsTheEmptyStateAndTheTargetStrip", () => {
    render(<PocList list={list([])} showSettings />);
    expect(screen.getByTestId("pocs-empty")).toHaveTextContent("No PoCs yet");
    expect(screen.getByTestId("pocs-target-main")).toHaveTextContent("github/poc-source");
    expect(screen.getByTestId("pocs-target-main")).toHaveTextContent("pocs/");
    expect(screen.getByTestId("pocs-target-settings")).toHaveAttribute("href", "/config/settings/pocs");
    expect(screen.queryByTestId("pocs-pills")).not.toBeInTheDocument();
  });

  it("PocList_WithoutConfigRead_HidesTargetSettings", () => {
    render(<PocList list={list([])} showSettings={false} />);
    expect(screen.getByTestId("pocs-targets")).toBeInTheDocument();
    expect(screen.queryByTestId("pocs-target-settings")).not.toBeInTheDocument();
  });

  it("PocList_ImportButton_OpensTheImportPage_AndOnlyForWhoMayImport", () => {
    const view = render(<PocList list={list([])} showSettings />);
    expect(screen.getByTestId("pocs-import")).toHaveAttribute("href", "/pocs/import");
    expect(screen.getByTestId("pocs-import")).not.toHaveAttribute("aria-disabled");
    view.unmount();
    render(<PocList list={list([], false)} showSettings />);
    expect(screen.queryByTestId("pocs-import")).not.toBeInTheDocument();
  });

  it("PocList_Cards_ShowNameHostStateOwnerAndRun_AndPillsNarrowThem", () => {
    render(<PocList list={list([poc(1, "shift-planner", "ready", "r-9"), poc(2, "doc-search", "blocked")])} showSettings />);
    expect(screen.getByTestId("poc-card-1")).toHaveTextContent("shift-planner.poc.example.internal");
    expect(screen.getByTestId("poc-card-1")).toHaveTextContent("Ready");
    expect(screen.getByTestId("poc-card-1")).toHaveTextContent("by Ann");
    expect(screen.getByTestId("poc-card-1-run")).toHaveAttribute("href", "/jobs/r-9");
    // 2026-11-23-c81ed: the name links to the PoC's detail page.
    expect(screen.getByTestId("poc-card-1-detail")).toHaveAttribute("href", "/pocs/1");
    expect(screen.getByTestId("poc-card-1-detail")).toHaveTextContent("shift-planner");
    expect(screen.getByTestId("pocs-pill-all")).toHaveTextContent("2");

    fireEvent.click(screen.getByTestId("pocs-pill-blocked"));

    expect(screen.queryByTestId("poc-card-1")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-card-2")).toBeInTheDocument();
  });

  // 2026-11-23-933dc: a blocked PoC links to its latest revision, which says why; any other state does not.
  it("PocList_BlockedCard_LinksToItsLatestRevision", () => {
    render(<PocList list={list([poc(4, "doc-search", "blocked", "r-1", 3), poc(5, "shift-planner", "ready", null, 2)])} showSettings />);
    expect(screen.getByTestId("poc-card-4-why")).toHaveAttribute("href", "/pocs/4/revisions/3");
    expect(screen.getByTestId("poc-card-4-why")).toHaveTextContent("See why it is blocked");
    expect(screen.queryByTestId("poc-card-5-why")).not.toBeInTheDocument();
  });

  // 2026-11-23-c81ee: a card carries "Expires in N days" while the reminder window is open and "Expired" once it expired.
  it("PocList_LifetimeBadge_ExpiringAndExpired_NothingOtherwise", () => {
    const life = (over: Partial<PocLifetime>): PocLifetime => ({
      expiresAt: "2026-12-07T12:00:00Z", daysLeft: 13, windowOpen: false, expired: false, deletesAt: null, extensions: 0,
      ownerAllowance: 2, mayExtend: true, ...over,
    });
    render(<PocList list={list([
      { ...poc(1, "shift-planner", "ready"), lifetime: life({ windowOpen: true, daysLeft: 6 }) },
      { ...poc(2, "doc-search", "stopped"), lifetime: life({ expired: true, daysLeft: 0 }) },
      { ...poc(3, "todo", "ready"), lifetime: life({ daysLeft: 60 }) },
      poc(5, "old", "deleted"),
    ])} showSettings />);
    expect(screen.getByTestId("poc-card-1-lifetime")).toHaveTextContent("Expires in 6 days");
    expect(screen.getByTestId("poc-card-2-lifetime")).toHaveTextContent("Expired");
    expect(screen.getByTestId("poc-card-2-lifetime")).toHaveAttribute("data-lifetime", "expired");
    expect(screen.queryByTestId("poc-card-3-lifetime")).not.toBeInTheDocument();
    expect(screen.queryByTestId("poc-card-5-lifetime")).not.toBeInTheDocument();
  });
});
