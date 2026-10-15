import { describe, expect, it } from "vitest";
import type { TicketGraphEdge, TicketGraphNode } from "@/types/tickets";
import { layersOf, layoutTicketGraph, NODE_W } from "../ticketGraphLayout";

// 2026-10-14-dbcen: the requires graph's pure layered layout.

const node = (id: string): TicketGraphNode => ({
  id, project: "shop", ticketId: id, key: `#${id}`, title: id, state: "ready", bucket: "filed", hasPage: true,
});
const edge = (from: string, to: string): TicketGraphEdge => ({ from, to });
const layers = (nodes: TicketGraphNode[], edges: TicketGraphEdge[]) =>
  Object.fromEntries(layersOf(nodes, edges).map(({ node: n, layer }) => [n.id, layer]));

describe("ticketGraphLayout", () => {
  it("a chain lays out in increasing layers", () => {
    expect(layers([node("c"), node("b"), node("a")], [edge("c", "b"), edge("b", "a")])).toEqual({ a: 0, b: 1, c: 2 });
  });

  it("a diamond puts the join one layer past its longest input", () => {
    const nodes = [node("a"), node("b"), node("c"), node("d")];
    const edges = [edge("b", "a"), edge("c", "b"), edge("d", "a"), edge("d", "c")];
    expect(layers(nodes, edges)).toEqual({ a: 0, b: 1, c: 2, d: 3 });
  });

  it("ties keep the Kahn order and every arrow points from the dependent's left to the required ticket's right", () => {
    const layout = layoutTicketGraph([node("x"), node("y"), node("z")], [edge("z", "x"), edge("z", "y")]);
    const at = Object.fromEntries(layout.nodes.map((n) => [n.node.id, n]));
    expect([at.x.layer, at.y.layer, at.z.layer]).toEqual([0, 0, 1]);
    expect(at.x.y).toBeLessThan(at.y.y);
    expect(layout.edges[0].path.startsWith(`M${at.z.x},`)).toBe(true);
    expect(layout.edges[0].path.endsWith(`${at.x.x + NODE_W},${at.x.y + 28}`)).toBe(true);
  });

  it("a cycle is laid one layer past the rest instead of dropped", () => {
    expect(layers([node("a"), node("p"), node("q")], [edge("p", "q"), edge("q", "p")])).toEqual({ a: 0, p: 1, q: 1 });
  });
});
