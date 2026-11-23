import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SeriesRunRow } from "@/types/seriesRuns";

// 2026-11-23-31dcc: the series list — one row per series with its next step and progress, a project filter this
// browser remembers, and an empty list that says what fills it.

const hoisted = vi.hoisted(() => ({ runs: vi.fn(), projects: vi.fn() }));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRuns: (...a: unknown[]) => hoisted.runs(...a) }));
vi.mock("@/lib/readableProjectsApi", () => ({ fetchReadableProjects: (...a: unknown[]) => hoisted.projects(...a) }));

import { SeriesRunsHome } from "../SeriesRunsHome";

function row(over: Partial<SeriesRunRow> = {}): SeriesRunRow {
  return {
    id: "sr-1", name: "Approval workflow", project: "shop", state: "running",
    counts: { total: 25, landed: 2, waiting: 15, held: 1, notStarted: 7 },
    nextMerge: "#48", question: null, lastActivityAt: new Date().toISOString(), ...over,
  };
}

function stubStorage(): Map<string, string> {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: { getItem: (k: string) => backing.get(k) ?? null, setItem: (k: string, v: string) => void backing.set(k, v) },
  });
  return backing;
}

beforeEach(() => {
  hoisted.runs.mockReset().mockResolvedValue([row()]);
  hoisted.projects.mockReset().mockResolvedValue(["shop", "billing"]);
});

describe("SeriesRunsHome", () => {
  it("SeriesList_ARow_ShowsNameProjectNextStepProgressAndHeld", async () => {
    stubStorage();
    render(<SeriesRunsHome />);

    const shown = await screen.findByTestId("series-row-sr-1");
    expect(shown).toHaveAttribute("href", "/series/sr-1");
    expect(shown).toHaveTextContent("Approval workflow");
    expect(shown).toHaveTextContent("shop · running");
    expect(screen.getByTestId("series-next")).toHaveTextContent("Merge #48 next");
    expect(shown).toHaveTextContent("2 of 25 merged · 15 PRs waiting · 7 not started");
    expect(screen.getByTestId("series-held")).toHaveTextContent("1 held");
  });

  it("SeriesList_ProjectFilter_AsksTheServerForThatProject_AndIsRemembered", async () => {
    const storage = stubStorage();
    render(<SeriesRunsHome />);
    await screen.findByRole("option", { name: "billing" });

    fireEvent.change(screen.getByTestId("series-project"), { target: { value: "billing" } });

    await waitFor(() => expect(hoisted.runs).toHaveBeenLastCalledWith("billing", expect.anything()));
    expect(storage.get("agentsmith.series-project")).toBe("billing");
  });

  it("SeriesList_ARememberedProject_IsTheFirstRead", async () => {
    const storage = stubStorage();
    storage.set("agentsmith.series-project", "shop");
    render(<SeriesRunsHome />);

    await waitFor(() => expect(hoisted.runs).toHaveBeenCalledTimes(1));
    expect(hoisted.runs).toHaveBeenCalledWith("shop", expect.anything());
  });

  it("SeriesList_Empty_SaysWhatFillsIt", async () => {
    stubStorage();
    hoisted.runs.mockResolvedValue([]);
    render(<SeriesRunsHome />);

    expect(await screen.findByTestId("series-empty")).toHaveTextContent("A series appears when a conversation files its tickets.");
  });
});
