import type { SeriesRunDetail, SeriesRunEdge, SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-23-31dcf: where the series graph puts each ticket — one row band (lane) per repo in the project's
// repo order, then one for tickets without a pull request yet; one column per depth, the order work can happen. A ticket
// in more than one repo sits in its first and names the others. Within one lane and column tickets stack in series order.
// 2026-11-23-f562: the graph is the Overview's; edges run orthogonally with slightly rounded corners, no curves.
// 2026-11-27-ecf2b: a ticket sits on its lane repo (the server's: its spec's, the one carrying code first) — on its first
// pull request's repo only from a server that does not say; its pull requests are chips on the node, no "+ repos" text.

export const NODE_W = 150;
export const NODE_H = 46;
const COLUMN_W = 176;
const ROW_H = 58;
const LANE_HEAD = 26;
const LANE_GAP = 10;
const LEFT = 16;

export interface SeriesLane {
  /** The repo; null for the lane of tickets with no pull request yet. */
  repo: string | null;
  top: number;
}

export interface SeriesNode {
  ticket: SeriesRunTicket;
  lane: string | null;
  x: number;
  y: number;
}

export interface SeriesLine {
  edge: SeriesRunEdge;
  d: string;
}

export interface SeriesOrderLayout {
  lanes: SeriesLane[];
  nodes: SeriesNode[];
  lines: SeriesLine[];
  width: number;
  height: number;
}

export function seriesOrderLayout(detail: SeriesRunDetail): SeriesOrderLayout {
  const laneOf = (t: SeriesRunTicket): string | null => (t.laneRepo !== undefined ? t.laneRepo : t.repos[0] ?? null);
  const used = new Set<string | null>(detail.tickets.map(laneOf));
  const repos: (string | null)[] = [...detail.repos.filter((r) => used.has(r)), ...[...used].filter((r) => r !== null && !detail.repos.includes(r))];
  if (used.has(null)) repos.push(null);
  const slot = new Map<string, number>();
  const rows = new Map<string | null, number>();
  const cells = new Map<string, number>();
  detail.tickets.forEach((t) => {
    const cell = `${laneOf(t)}\u0000${t.depth}`;
    const at = cells.get(cell) ?? 0;
    cells.set(cell, at + 1);
    slot.set(t.ticketId, at);
    rows.set(laneOf(t), Math.max(rows.get(laneOf(t)) ?? 0, at + 1));
  });
  let y = 0;
  const lanes = repos.map((repo) => {
    const lane = { repo, top: y };
    y += LANE_HEAD + (rows.get(repo) ?? 0) * ROW_H + LANE_GAP;
    return lane;
  });
  const top = new Map(lanes.map((l) => [l.repo, l.top]));
  const nodes = detail.tickets.map((ticket) => ({
    ticket, lane: laneOf(ticket),
    x: LEFT + ticket.depth * COLUMN_W, y: (top.get(laneOf(ticket)) ?? 0) + LANE_HEAD + (slot.get(ticket.ticketId) ?? 0) * ROW_H,
  }));
  const columns = Math.max(0, ...detail.tickets.map((t) => t.depth)) + 1;
  return { lanes, nodes, lines: lines(detail.edges, nodes), width: LEFT + columns * COLUMN_W, height: y };
}

/** The DOM id of a ticket's node, so its menu can give focus back to it. */
export function seriesNodeId(ticketId: string): string {
  return `sr-node-${ticketId.replace(/[^A-Za-z0-9-]/g, (c) => `_${c.charCodeAt(0)}_`)}`;
}

/** 2026-11-26-5201e: the element holding a node's links, beside its button — a click there is still on the node. */
export function seriesNodeLinksId(ticketId: string): string {
  return `${seriesNodeId(ticketId)}-links`;
}

const CORNER = 6;
const BEND_BEFORE = 12;

/** An orthogonal path from (x1, y1) to (x2, y2): out horizontally, up or down just before the target's column, in
 *  horizontally — its two corners rounded by a few pixels; a straight line when both ends are level. */
export function orthogonalPath(x1: number, y1: number, x2: number, y2: number): string {
  if (Math.abs(y2 - y1) < 1) return `M${x1},${y1} H${x2}`;
  const bend = x2 - BEND_BEFORE;
  const sign = y2 > y1 ? 1 : -1;
  const r = Math.min(CORNER, Math.abs(y2 - y1) / 2, Math.max(0, bend - x1));
  return `M${x1},${y1} H${bend - r} Q${bend},${y1} ${bend},${y1 + sign * r} V${y2 - sign * r} Q${bend},${y2} ${bend + r},${y2} H${x2}`;
}

// A path from the prerequisite's right edge to the dependent's left edge, for every edge whose two ends are drawn.
function lines(edges: SeriesRunEdge[], nodes: SeriesNode[]): SeriesLine[] {
  const at = new Map(nodes.map((n) => [n.ticket.ticketId, n]));
  return edges.flatMap((edge) => {
    const from = at.get(edge.requiredTicketId);
    const to = at.get(edge.ticketId);
    if (!from || !to) return [];
    return [{ edge, d: orthogonalPath(from.x + NODE_W, from.y + NODE_H / 2, to.x, to.y + NODE_H / 2) }];
  });
}

/** The ticket, every ticket it requires (upstream) and every ticket requiring it (downstream), over the series' edges. */
export function seriesCourse(edges: SeriesRunEdge[], ticketId: string): Set<string> {
  const walk = (start: string, next: (id: string) => string[]) => {
    const seen = new Set([start]);
    for (const queue = [start]; queue.length > 0;) {
      for (const id of next(queue.shift()!)) if (!seen.has(id)) { seen.add(id); queue.push(id); }
    }
    return seen;
  };
  const up = walk(ticketId, (id) => edges.filter((e) => e.ticketId === id).map((e) => e.requiredTicketId));
  const down = walk(ticketId, (id) => edges.filter((e) => e.requiredTicketId === id).map((e) => e.ticketId));
  return new Set([...up, ...down]);
}
