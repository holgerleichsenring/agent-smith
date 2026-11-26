import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CHAT_LINES_PX, GRAPH_MIN_PX, seriesGraphBounds, seriesGraphHeight } from "../useSeriesGraphSplit";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-26-f511b: the divider between the series graph and the chat — its bounds, its keys, and the share remembered per
// browser. jsdom lays nothing out, so the page's tests see an unmeasured pane: the divider then reads the stored share.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn() }));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a) }));
vi.mock("@/hooks/useSpecDialog", () => ({
  useSpecDialog: () => ({ entries: [], working: false, readings: [], activity: [], workingSince: null, uploadNote: null, failure: null,
    awaitingProposal: null, question: null, view: { session: { sessionId: "conv-1" } }, send: vi.fn(), attach: vi.fn() }),
}));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

import { SeriesRunPage } from "../SeriesRunPage";

describe("seriesGraphHeight", () => {
  it("SeriesGraphHeight_TheShare_CappedByTheGraphsOwnHeight_AndTheChatsRoom", () => {
    expect(seriesGraphHeight(0.4, { pane: 1000, natural: 900, chatFixed: 200 })).toBe(400);
    expect(seriesGraphHeight(0.4, { pane: 1000, natural: 250, chatFixed: 200 })).toBe(250);
    expect(seriesGraphHeight(0.9, { pane: 1000, natural: 2000, chatFixed: 300 })).toBe(1000 - 300 - CHAT_LINES_PX);
  });

  it("SeriesGraphHeight_WhenTheBoundsCollide_TheGraphsHeadWins", () => {
    expect(seriesGraphBounds({ pane: 300, natural: 600, chatFixed: 250 })).toEqual({ min: GRAPH_MIN_PX, max: GRAPH_MIN_PX });
    expect(seriesGraphHeight(0.01, { pane: 1000, natural: 600, chatFixed: 100 })).toBe(GRAPH_MIN_PX);
  });
});

describe("the divider", () => {
  const original = Object.getOwnPropertyDescriptor(window, "localStorage");
  afterEach(() => { if (original) Object.defineProperty(window, "localStorage", original); });
  beforeEach(() => hoisted.fetch.mockReset().mockResolvedValue(overviewDetail()));

  function storage(initial: Record<string, string> = {}, broken = false): Map<string, string> {
    const backing = new Map(Object.entries(initial));
    Object.defineProperty(window, "localStorage", {
      configurable: true,
      value: {
        getItem: (k: string) => { if (broken) throw new Error("blocked"); return backing.get(k) ?? null; },
        setItem: (k: string, v: string) => { if (broken) throw new Error("blocked"); backing.set(k, v); },
      },
    });
    return backing;
  }

  it("SeriesDivider_IsAFocusableSeparator_ArrowDownGrowsTheGraph_AndIsRemembered", async () => {
    const backing = storage();
    const first = render(<SeriesRunPage id="sr-1" />);
    const divider = await screen.findByTestId("series-graph-divider");
    expect(divider).toHaveAttribute("role", "separator");
    expect(divider).toHaveAttribute("tabindex", "0");
    expect(divider).toHaveAttribute("aria-orientation", "horizontal");
    expect(divider).toHaveAttribute("aria-controls", "series-overview");
    expect(divider).toHaveAttribute("aria-valuenow", "40");

    fireEvent.keyDown(divider, { key: "ArrowDown" });
    expect(divider).toHaveAttribute("aria-valuenow", "45");
    fireEvent.keyDown(divider, { key: "ArrowUp" });
    fireEvent.keyDown(divider, { key: "ArrowUp" });
    expect(divider).toHaveAttribute("aria-valuenow", "35");
    expect(backing.get("agentsmith.series-graph-split")).toBe("0.350");

    first.unmount();
    render(<SeriesRunPage id="sr-1" />);
    const again = await screen.findByTestId("series-graph-divider");
    await waitFor(() => expect(again).toHaveAttribute("aria-valuenow", "35"));
  });

  it("SeriesDivider_ABlockedStorage_KeepsTheDefault_AndStillMoves", async () => {
    storage({}, true);
    render(<SeriesRunPage id="sr-1" />);
    const divider = await screen.findByTestId("series-graph-divider");

    expect(divider).toHaveAttribute("aria-valuenow", "40");
    fireEvent.keyDown(divider, { key: "End" });
    expect(divider).toHaveAttribute("aria-valuenow", "90");
  });

  it("SeriesDivider_AFoldedGraph_HasNoDivider", async () => {
    storage({ "agentsmith.series-graph-collapsed": "1" });
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-graph-collapsed")).toBeInTheDocument();
    expect(screen.queryByTestId("series-graph-divider")).toBeNull();
  });
});
