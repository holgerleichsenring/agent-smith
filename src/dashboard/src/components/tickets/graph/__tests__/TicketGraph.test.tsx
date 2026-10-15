import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketGraph as Graph, TicketGraphNode } from "@/types/tickets";

// 2026-10-14-dbcen: the Graph view on /tickets — nodes in their states, an edge from each dependent, a node with a
// page opens it and a page-less node opens its requires panel with the act.

// 2026-10-15-4a4de: heldIn, when set, answers per project — a call site that drops the project holds nothing.
const hoisted = vi.hoisted(() => ({ graph: vi.fn(), mark: vi.fn(), push: vi.fn(), lacks: false, heldIn: null as Record<string, string[]> | null }));
// 2026-10-15-4a4de: whether this sign-in reaches a project is useProjectReach's question; here it is an answer.
const reach = vi.hoisted(() => ({ value: "unknown" as "none" | "unknown" }));
vi.mock("@/hooks/useProjectReach", () => ({ useProjectReach: () => reach.value }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: hoisted.push }) }));
vi.mock("@/lib/ticketGraphApi", () => ({
  fetchTicketGraph: (...args: unknown[]) => hoisted.graph(...args),
  markLandedByHand: (...args: unknown[]) => hoisted.mark(...args),
}));
vi.mock("@/hooks/useLacksPermission", () => ({
  useLacksPermission: (permission: string, project?: string) =>
    hoisted.heldIn ? !(hoisted.heldIn[project ?? ""] ?? []).includes(permission) : hoisted.lacks,
}));

const { TicketGraph } = await import("../TicketGraph");

const node = (ticketId: string, state: string, hasPage = true): TicketGraphNode => ({
  id: `gh:github-${ticketId}`, project: "shop", ticketId, key: `#${ticketId}`, title: hasPage ? `Ticket ${ticketId}` : `#${ticketId}`,
  state, bucket: hasPage ? "filed" : "idle", hasPage,
});

const graph: Graph = {
  nodes: [node("59", "blocked"), node("58", "merged"), node("41", "idle", false)],
  edges: [{ from: "gh:github-59", to: "gh:github-58" }, { from: "gh:github-59", to: "gh:github-41" }],
};

beforeEach(() => {
  hoisted.graph.mockReset().mockResolvedValue(graph);
  hoisted.mark.mockReset();
  hoisted.push.mockReset();
  hoisted.lacks = false;
  hoisted.heldIn = null;
  reach.value = "unknown";
});

async function open() {
  render(<div className="mock-tickets"><TicketGraph /></div>);
  return screen.findByTestId("ticket-graph");
}

describe("TicketGraph", () => {
  it("TicketGraph_DrawsEveryNodeInItsStateAndAnEdgePerRequires", async () => {
    await open();
    expect(screen.getByTestId("ticket-graph-node-59").getAttribute("class")).toBe("tg-node blocked");
    expect(screen.getByTestId("ticket-graph-node-58").getAttribute("class")).toBe("tg-node merged");
    expect(screen.getByTestId("ticket-graph-node-41")).toHaveTextContent("no run · no page");
    expect(screen.getAllByTestId("ticket-graph-edge")).toHaveLength(2);
  });

  it("TicketGraph_NodeWithAPage_OpensIt", async () => {
    await open();
    fireEvent.click(screen.getByTestId("ticket-graph-node-58"));
    expect(hoisted.push).toHaveBeenCalledWith("/tickets/shop/58");
    expect(screen.queryByTestId("ticket-graph-panel-58")).toBeNull();
  });

  it("TicketGraph_NodeWithoutAPage_OffersTheAct", async () => {
    hoisted.mark.mockResolvedValue({ kind: "landed" });
    await open();
    fireEvent.keyDown(screen.getByTestId("ticket-graph-node-41"), { key: "Enter" });

    const panel = screen.getByTestId("ticket-graph-panel-41");
    expect(panel).toHaveTextContent("Required by #59");
    fireEvent.click(within(panel).getByTestId("done-by-hand-41"));
    fireEvent.change(screen.getByTestId("done-by-hand-note"), { target: { value: "merged elsewhere" } });
    await act(async () => fireEvent.click(screen.getByTestId("done-by-hand-send")));

    expect(hoisted.mark).toHaveBeenCalledWith("shop", "41", "merged elsewhere");
    expect(hoisted.graph).toHaveBeenCalledTimes(2);
  });

  // 2026-10-15-114fd
  it("TicketGraph_FailedPrClosedAndClosedNodes_ShowTheirWordsAndLegend", async () => {
    hoisted.graph.mockResolvedValue({
      nodes: [node("60", "blocked"), node("61", "failed"), node("62", "pr_closed"), node("63", "closed")],
      edges: ["61", "62", "63"].map((id) => ({ from: "gh:github-60", to: `gh:github-${id}` })),
    });
    await open();

    for (const [id, state, words] of [["61", "failed", "failed"], ["62", "pr_closed", "PR closed"], ["63", "closed", "closed"]]) {
      const drawn = screen.getByTestId(`ticket-graph-node-${id}`);
      expect(drawn.getAttribute("class")).toBe(`tg-node ${state}`);
      expect(drawn.querySelector(".tg-state")).toHaveTextContent(words);
    }
    const legend = document.querySelector(".tg-legend") as HTMLElement;
    for (const cls of ["failed", "pr_closed", "closed"]) expect(legend.querySelector(`i.${cls}`), cls).not.toBeNull();
    expect(legend).toHaveTextContent("PR closed");
  });

  it("RequiresPanel_DoneByHand_GatedOnTheNodesProject", async () => {
    hoisted.heldIn = { other: ["runs.control"] };
    const { unmount } = render(<TicketGraph />);
    fireEvent.keyDown(await screen.findByTestId("ticket-graph-node-41"), { key: "Enter" });
    expect(within(screen.getByTestId("ticket-graph-panel-41")).queryByTestId("done-by-hand-41")).toBeNull();
    unmount();

    hoisted.heldIn = { shop: ["runs.control"] };
    render(<TicketGraph />);
    fireEvent.keyDown(await screen.findByTestId("ticket-graph-node-41"), { key: "Enter" });
    expect(within(screen.getByTestId("ticket-graph-panel-41")).getByTestId("done-by-hand-41")).toBeInTheDocument();
  });

  it("EmptyAndNoProjectReached_SaysSo", async () => {
    reach.value = "none";
    hoisted.graph.mockResolvedValue({ nodes: [], edges: [] });
    render(<TicketGraph />);
    expect(await screen.findByTestId("no-project-reach")).toHaveTextContent("Your sign-in reaches no project on this installation");
    expect(screen.queryByText(/No ticket requires another yet/)).toBeNull();
  });

  it("EmptyForAGlobalReader_KeepsTodaysText", async () => {
    hoisted.graph.mockResolvedValue({ nodes: [], edges: [] });
    render(<TicketGraph />);
    expect(await screen.findByTestId("ticket-graph-empty")).toHaveTextContent(/No ticket requires another yet/);
    expect(screen.queryByTestId("no-project-reach")).toBeNull();
  });

  it("TicketGraph_NoRequiresYet_SaysSo", async () => {
    hoisted.graph.mockResolvedValue({ nodes: [], edges: [] });
    render(<TicketGraph />);
    expect(await screen.findByTestId("ticket-graph-empty")).toHaveTextContent("No ticket requires another yet");
  });
});
