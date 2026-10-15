"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { refusalIn, type ApiRefusal } from "@/lib/apiResponse";
import { fetchTicketGraph } from "@/lib/ticketGraphApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketGraph as Graph, TicketGraphNode } from "@/types/tickets";
import { TicketRequiresPanel } from "./TicketRequiresPanel";
import { layoutTicketGraph, NODE_H, NODE_W } from "./ticketGraphLayout";

// 2026-10-14-dbcen: /tickets?view=graph — every ticket that requires or is required, an arrow from each dependent
// to what it requires, each node in its state (ticket-graph.html, graph-view). A node with a page opens it; a
// page-less one opens its requires panel with the act. Hand-drawn SVG over a pure layout, like the flow graph.

type Load = { kind: "loading" } | { kind: "failed"; refusal: ApiRefusal | null } | { kind: "read"; graph: Graph };

const STATE_WORDS: Record<string, string> = { "done-by-hand": "done by hand", waiting_on_review: "waiting on review", needs_you: "needs you" };
const LEGEND: ReadonlyArray<readonly [string, string]> = [
  ["merged", "merged or done by hand"], ["running", "running"], ["blocked", "blocked"], ["ready", "ready"], ["idle", "no run, no page"],
];

export function TicketGraph({ project }: { project?: string | null }) {
  const router = useRouter();
  const [load, setLoad] = useState<Load>({ kind: "loading" });
  const [chosen, setChosen] = useState<string | null>(null);
  const read = useCallback(() => {
    fetchTicketGraph(project)
      .then((graph) => setLoad({ kind: "read", graph }))
      .catch((thrown: unknown) => setLoad({ kind: "failed", refusal: refusalIn(thrown) }));
  }, [project]);
  useEffect(read, [read]);
  const graph = load.kind === "read" ? load.graph : null;
  const layout = useMemo(() => (graph ? layoutTicketGraph(graph.nodes, graph.edges) : null), [graph]);

  if (load.kind === "failed" && load.refusal) return <RefusalSurface refusal={load.refusal} surface="the ticket graph" />;
  if (load.kind === "failed") return <div className="msub" data-testid="ticket-graph-failed">The ticket graph could not be read.</div>;
  if (!graph || !layout) return <div className="msub" data-testid="ticket-graph-loading">Reading the ticket graph…</div>;
  if (graph.nodes.length === 0) {
    return <div className="msub" data-testid="ticket-graph-empty">No ticket requires another yet. Requires are filed with a list of tickets.</div>;
  }
  const open = (node: TicketGraphNode) => (node.hasPage ? router.push(ticketPath(node.project, node.ticketId)) : setChosen(node.id));
  const panel = graph.nodes.find((n) => n.id === chosen && !n.hasPage) ?? null;
  return (
    <div data-testid="ticket-graph">
      <div className="tgraph">
        <svg className="tg-canvas" viewBox={`0 0 ${layout.width} ${layout.height}`} width={layout.width} height={layout.height}
          role="img" aria-label="Tickets and what they require">
          <defs>
            <marker id="tg-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
              <path className="tg-arrow" d="M0,0 L10,5 L0,10 z" />
            </marker>
          </defs>
          {layout.edges.map(({ edge, path }) => (
            <path key={`${edge.from}>${edge.to}`} className="tg-edge" d={path} markerEnd="url(#tg-arrow)" data-testid="ticket-graph-edge" />
          ))}
          {layout.nodes.map(({ node, x, y }) => (
            <g key={node.id} className={`tg-node ${node.state}`} transform={`translate(${x},${y})`} tabIndex={0} role="button"
              aria-label={`${node.key} ${node.title}, ${STATE_WORDS[node.state] ?? node.state}`} data-testid={`ticket-graph-node-${node.ticketId}`}
              onClick={() => open(node)} onKeyDown={(e) => e.key === "Enter" && open(node)}>
              <rect width={NODE_W} height={NODE_H} rx={9} />
              <text className="tg-key" x={12} y={18}>{node.key}</text>
              <text className="tg-title" x={12} y={34}>{clip(node.title)}</text>
              <text className="tg-state" x={12} y={49}>{node.hasPage ? STATE_WORDS[node.state] ?? node.state : "no run · no page"}</text>
            </g>
          ))}
        </svg>
        <div className="tg-legend">
          {LEGEND.map(([cls, words]) => <span key={cls}><i className={cls} />{words}</span>)}
        </div>
      </div>
      {panel && (
        <TicketRequiresPanel node={panel} onLanded={read}
          requiredBy={graph.edges.filter((e) => e.to === panel.id).flatMap((e) => graph.nodes.filter((n) => n.id === e.from))} />
      )}
    </div>
  );
}

/** A title that fits the node's width at its font size. */
function clip(title: string): string {
  return title.length > 28 ? `${title.slice(0, 27)}…` : title;
}
