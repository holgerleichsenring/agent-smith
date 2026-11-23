import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { PocList as PocListAnswer, PocRow } from "@/lib/pocsApi";
import { PocList } from "../PocList";

// 2026-11-23-933da: the PoCs list — the empty state, the target strip with its settings link for config readers, the
// cards and the state pills that narrow them.

const TARGETS = [{ name: "main", sourceRepo: "github/poc-source", root: "pocs" }];

function poc(id: number, name: string, state: string, lastRunId: string | null = null): PocRow {
  return { id, name, project: "poc-ann", target: "main", host: `${name}.poc.example.internal`, state, ownerName: "Ann", lastRunId };
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

  it("PocList_ImportButton_IsDisabledUntilImportArrives_AndOnlyForWhoMayImport", () => {
    const view = render(<PocList list={list([])} showSettings />);
    expect(screen.getByTestId("pocs-import")).toBeDisabled();
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
    expect(screen.getByTestId("pocs-pill-all")).toHaveTextContent("2");

    fireEvent.click(screen.getByTestId("pocs-pill-blocked"));

    expect(screen.queryByTestId("poc-card-1")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-card-2")).toBeInTheDocument();
  });
});
