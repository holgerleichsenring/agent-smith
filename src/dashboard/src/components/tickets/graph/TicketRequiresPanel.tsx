"use client";

import type { TicketGraphNode } from "@/types/tickets";
import { DoneByHandAct } from "../landing/DoneByHandAct";

// 2026-10-14-dbcen: a graph node without a page — a person's ticket named by ticket:<id>, a filing from before
// projects were recorded, a ticket whose runs ran elsewhere. Its key, its title, what requires it, and the act.

const LANDED = new Set(["merged", "done-by-hand"]);

export function TicketRequiresPanel({ node, requiredBy, onLanded }: {
  node: TicketGraphNode; requiredBy: TicketGraphNode[]; onLanded: () => void;
}) {
  return (
    <div className="tg-panel" role="region" aria-label={node.key} data-testid={`ticket-graph-panel-${node.ticketId}`}>
      <span className="key">{node.key}</span>
      <span className="pt">{node.title}</span>
      <span className="pr">
        No run and no filing here — the ticket lives only on the tracker.
        {requiredBy.length > 0 && <> Required by <b>{requiredBy.map((n) => n.key).join(", ")}</b>.</>}
      </span>
      <div>
        <DoneByHandAct project={node.project} ticketId={node.ticketId} label={node.key} landed={LANDED.has(node.state)} onLanded={onLanded} />
      </div>
    </div>
  );
}
