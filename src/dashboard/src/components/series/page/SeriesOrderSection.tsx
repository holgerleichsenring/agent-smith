"use client";

import { useMemo, useState } from "react";
import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { seriesNodeFrame } from "./seriesNodeFrame";
import { NODE_H, NODE_W, seriesCourse, seriesOrderLayout } from "./seriesOrderLayout";
import { seriesTicketState } from "./seriesTicketState";

// 2026-11-23-31dcf: the series' tickets in repo lanes by the order work can happen, every requires edge drawn as what it
// means for the branches (the kind the server reads from the per-repo stack). Picking a ticket — in the select or by a
// click on it — keeps only its course lit: what it requires and what requires it; a second click lets go. The graph scrolls sideways in its box.

export function SeriesOrderSection({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const layout = useMemo(() => seriesOrderLayout(detail), [detail]);
  const [from, setFrom] = useState<string | null>(null);
  const course = useMemo(() => (from ? seriesCourse(detail.edges, from) : null), [detail.edges, from]);
  const lit = (id: string) => !course || course.has(id);
  return (
    <>
      <p className="msub">{t("series.order.sub")}</p>
      <div className="sr-order-tools">
        <label htmlFor="sr-order-from">{t("series.order.from")}</label>
        <select id="sr-order-from" value={from ?? ""} onChange={(e) => setFrom(e.target.value || null)} data-testid="series-order-from">
          <option value="">{t("series.order.all", { count: detail.tickets.length })}</option>
          {detail.tickets.map((ticket) => <option key={ticket.ticketId} value={ticket.ticketId}>{ticket.key} {ticket.title}</option>)}
        </select>
        <button type="button" className="sr-order-reset" onClick={() => setFrom(null)} data-testid="series-order-reset">{t("series.order.reset")}</button>
      </div>
      <div className="sr-order-legend">
        {(["stacked", "merge", "unknown", "landed"] as const).map((kind) => (
          <span key={kind}><i className={`sr-edge-key ${kind}`} />{t(`series.order.edge.${kind}`)}</span>
        ))}
        {(["waiting", "next", "question", "notStarted", "merged"] as const).map((frame) => (
          <span key={frame}><i className={`sr-frame-key ${frame}`} />{t(`series.order.frame.${frame}`)}</span>
        ))}
      </div>
      <div className="sr-order-box" data-testid="series-order-graph">
        <div className="sr-order-graph" style={{ width: layout.width, height: layout.height }}>
          {layout.lanes.map((lane) => (
            <div key={lane.repo ?? ""} className="sr-lane" style={{ top: lane.top }} data-testid={`series-lane-${lane.repo ?? "none"}`}>
              <span>{lane.repo ?? t("series.order.noRepo")}</span>
            </div>
          ))}
          <svg width={layout.width} height={layout.height} aria-hidden="true">
            {layout.lines.map(({ edge, d }) => (
              <path key={`${edge.requiredTicketId}>${edge.ticketId}`} d={d}
                className={`sr-edge ${edge.kind}${lit(edge.ticketId) && lit(edge.requiredTicketId) ? "" : " dim"}`}
                data-testid={`series-edge-${edge.requiredTicketId}-${edge.ticketId}`} />
            ))}
          </svg>
          {layout.nodes.map(({ ticket, alsoIn, x, y }) => (
            <button key={ticket.ticketId} type="button" style={{ left: x, top: y, width: NODE_W, height: NODE_H }}
              className={`sr-node ${seriesNodeFrame(ticket, detail)}${lit(ticket.ticketId) ? "" : " dim"}`}
              title={`${ticket.key} ${ticket.title} · ${seriesTicketState(ticket, t).text}`}
              aria-label={`${ticket.key} ${ticket.title} · ${seriesTicketState(ticket, t).text}`} aria-pressed={from === ticket.ticketId}
              onClick={() => setFrom(from === ticket.ticketId ? null : ticket.ticketId)} data-testid={`series-node-${ticket.ticketId}`}>
              <span className="sr-node-id">{ticket.key}{alsoIn.length > 0 ? ` · ${t("series.order.alsoIn", { repos: alsoIn.join(" + ") })}` : ""}</span>
              <span className="sr-node-name">{ticket.title}</span>
            </button>
          ))}
        </div>
      </div>
    </>
  );
}
