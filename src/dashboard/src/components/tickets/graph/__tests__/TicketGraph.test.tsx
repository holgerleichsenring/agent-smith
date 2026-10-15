import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketGraph as Graph, TicketGraphNode } from "@/types/tickets";

// 2026-10-14-dbcen: the Graph view on /tickets — nodes in their states, an edge from each dependent, a node with a
// page opens it and a page-less node opens its requires panel with the act.

const hoisted = vi.hoisted(() => ({ graph: vi.fn(), mark: vi.fn(), push: vi.fn(), lacks: false }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: hoisted.push }) }));
vi.mock("@/lib/ticketGraphApi", () => ({
  fetchTicketGraph: (...args: unknown[]) => hoisted.graph(...args),
  markLandedByHand: (...args: unknown[]) => hoisted.mark(...args),
}));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));

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

  it("TicketGraph_NoRequiresYet_SaysSo", async () => {
    hoisted.graph.mockResolvedValue({ nodes: [], edges: [] });
    render(<TicketGraph />);
    expect(await screen.findByTestId("ticket-graph-empty")).toHaveTextContent("No ticket requires another yet");
  });
});
