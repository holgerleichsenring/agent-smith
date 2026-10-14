import type { TicketGraphEdge, TicketGraphNode } from "@/types/tickets";

// 2026-10-14-dbcen: the requires graph laid out in layers, pure. A ticket that requires nothing is layer 0; every
// other one sits one layer past the longest of what it requires, so an arrow (dependent -> required) always points
// left. Within a layer, tickets keep the Kahn order — required-first, ties in the server's node order. A cycle the
// server should never send is laid one layer past the rest rather than dropped.

export const NODE_W = 190;
export const NODE_H = 56;
const GAP_X = 90;
const GAP_Y = 20;
const PAD = 10;

export interface LaidNode {
  node: TicketGraphNode;
  layer: number;
  x: number;
  y: number;
}

export interface LaidEdge {
  edge: TicketGraphEdge;
  path: string;
}

export interface TicketGraphLayout {
  width: number;
  height: number;
  nodes: LaidNode[];
  edges: LaidEdge[];
}

/** Each node's layer, in Kahn order. */
export function layersOf(nodes: TicketGraphNode[], edges: TicketGraphEdge[]): Array<{ node: TicketGraphNode; layer: number }> {
  const ids = new Set(nodes.map((n) => n.id));
  const live = edges.filter((e) => ids.has(e.from) && ids.has(e.to) && e.from !== e.to);
  const waiting = new Map(nodes.map((n) => [n.id, live.filter((e) => e.from === n.id).length]));
  const layer = new Map(nodes.map((n) => [n.id, 0]));
  const queue = nodes.filter((n) => waiting.get(n.id) === 0);
  const order: TicketGraphNode[] = [];
  while (queue.length > 0) {
    const node = queue.shift()!;
    order.push(node);
    for (const edge of live.filter((e) => e.to === node.id)) {
      layer.set(edge.from, Math.max(layer.get(edge.from)!, layer.get(node.id)! + 1));
      const left = waiting.get(edge.from)! - 1;
      waiting.set(edge.from, left);
      if (left === 0) queue.push(nodes.find((n) => n.id === edge.from)!);
    }
  }
  const deepest = Math.max(0, ...order.map((n) => layer.get(n.id)!));
  const rest = nodes.filter((n) => !order.includes(n)).map((node) => ({ node, layer: deepest + 1 }));
  return [...order.map((node) => ({ node, layer: layer.get(node.id)! })), ...rest];
}

export function layoutTicketGraph(nodes: TicketGraphNode[], edges: TicketGraphEdge[]): TicketGraphLayout {
  const rows = new Map<number, number>();
  const laid = layersOf(nodes, edges).map(({ node, layer }) => {
    const row = rows.get(layer) ?? 0;
    rows.set(layer, row + 1);
    return { node, layer, x: PAD + layer * (NODE_W + GAP_X), y: PAD + row * (NODE_H + GAP_Y) };
  });
  const at = new Map(laid.map((n) => [n.node.id, n]));
  const lines = edges.flatMap((edge) => {
    const from = at.get(edge.from);
    const to = at.get(edge.to);
    return from && to ? [{ edge, path: curve(from, to) }] : [];
  });
  const layers = Math.max(0, ...laid.map((n) => n.layer)) + 1;
  const deepest = Math.max(0, ...rows.values());
  return {
    width: 2 * PAD + layers * NODE_W + (layers - 1) * GAP_X,
    height: 2 * PAD + deepest * NODE_H + Math.max(0, deepest - 1) * GAP_Y,
    nodes: laid,
    edges: lines,
  };
}

/** From the dependent's left edge to the required ticket's right edge. */
function curve(from: LaidNode, to: LaidNode): string {
  const x1 = from.x;
  const y1 = from.y + NODE_H / 2;
  const x2 = to.x + NODE_W;
  const y2 = to.y + NODE_H / 2;
  const dx = Math.max(24, (x1 - x2) / 2);
  return `M${x1},${y1} C${x1 - dx},${y1} ${x2 + dx},${y2} ${x2},${y2}`;
}
