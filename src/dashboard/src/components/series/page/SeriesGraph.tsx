"use client";

import { useLayoutEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useT } from "@/hooks/useT";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { SERIES_NODE_KINDS, seriesNeededChain, seriesNodeKind, waitingPullRequests } from "./seriesNodeKind";
import { NODE_H, NODE_W, seriesNodeId, seriesOrderLayout } from "./seriesOrderLayout";

// 2026-11-23-f562: the Overview's order graph — repo lanes, depth columns, every ticket in ONE colour by its hold
// (seriesNodeKind), edges orthogonal and the needed-merge chain's edges in the accent, a legend and the course select.
// Clicking a ticket opens its menu (drawn by the caller below the node, outside the scrolling box so it is never clipped);
// the graph scrolls sideways in its own box, and scrolling it closes the menu.

interface Props {
  detail: SeriesRunDetail;
  /** The tickets lit — a course or a hovered row's chain; null lights everything. */
  lit: Set<string> | null;
  from: string | null;
  onFrom: (ticketId: string | null) => void;
  openId: string | null;
  onNode: (ticketId: string) => void;
  onClose: () => void;
  /** Where the menu sits in the panel: just below the node. */
  menu: (ticket: SeriesRunTicket, at: { left: number; top: number }) => ReactNode;
}

const MENU_W = 260;

export function SeriesGraph({ detail, lit, from, onFrom, openId, onNode, onClose, menu }: Props) {
  const t = useT();
  const layout = useMemo(() => seriesOrderLayout(detail), [detail]);
  const chain = useMemo(() => seriesNeededChain(detail), [detail]);
  const on = (id: string) => !lit || lit.has(id);
  const open = layout.nodes.find((node) => node.ticket.ticketId === openId);
  const panel = useRef<HTMLElement>(null);
  const [at, setAt] = useState<{ left: number; top: number } | null>(null);
  useLayoutEffect(() => {
    const node = openId ? document.getElementById(seriesNodeId(openId)) : null;
    if (!node || !panel.current) { setAt(null); return; }
    const [box, own] = [panel.current.getBoundingClientRect(), node.getBoundingClientRect()];
    setAt({ left: Math.max(0, Math.min(own.left - box.left, box.width - MENU_W)), top: own.bottom - box.top + 6 });
  }, [openId, layout]);
  return (
    <section ref={panel} className="sr-graph-panel" aria-label={t("series.graph.title")}>
      <div className="sr-graph-head">
        <div className="sr-legend" data-testid="series-graph-legend">
          {SERIES_NODE_KINDS.map((kind) => <span key={kind}><i className={`sr-dot ${kind}`} />{t(`series.kind.${kind}`)}</span>)}
        </div>
        <div className="sr-graph-from">
          <label htmlFor="sr-graph-from">{t("series.graph.from")}</label>
          <select id="sr-graph-from" value={from ?? ""} onChange={(e) => onFrom(e.target.value || null)} data-testid="series-graph-from">
            <option value="">{t("series.graph.all", { count: detail.tickets.length })}</option>
            {detail.tickets.map((ticket) => <option key={ticket.ticketId} value={ticket.ticketId}>{ticket.key} {ticket.title}</option>)}
          </select>
        </div>
      </div>
      <div className="sr-graph-box" data-testid="series-graph" onScroll={() => { if (openId) onClose(); }}>
        <div className="sr-graph" style={{ width: layout.width, height: layout.height }}>
          {layout.lanes.map((lane) => (
            <div key={lane.repo ?? ""} className="sr-lane" style={{ top: lane.top }} data-testid={`series-lane-${lane.repo ?? "none"}`}>
              <span>{lane.repo ?? t("series.graph.noRepo")}</span>
            </div>
          ))}
          <svg width={layout.width} height={layout.height} aria-hidden="true">
            {layout.lines.map(({ edge, d }) => {
              const needed = chain.has(edge.ticketId) && chain.has(edge.requiredTicketId);
              return (
                <path key={`${edge.requiredTicketId}>${edge.ticketId}`} d={d}
                  className={`sr-edge${needed ? " needed" : ""}${on(edge.ticketId) && on(edge.requiredTicketId) ? "" : " dim"}`}
                  data-testid={`series-edge-${edge.requiredTicketId}>${edge.ticketId}`} />
              );
            })}
          </svg>
          {layout.nodes.map(({ ticket, alsoIn, x, y }) => {
            const kind = seriesNodeKind(ticket, detail);
            const pr = waitingPullRequests(ticket.pullRequests)[0] ?? (ticket.landed ? ticket.pullRequests[0] : undefined);
            return (
              <button key={ticket.ticketId} id={seriesNodeId(ticket.ticketId)} type="button" style={{ left: x, top: y, width: NODE_W, height: NODE_H }}
                className={`sr-node ${kind}${on(ticket.ticketId) ? "" : " dim"}`} data-kind={kind}
                aria-label={`${ticket.key} ${ticket.title}: ${t(`series.kind.${kind}`)}`} aria-haspopup="dialog"
                aria-expanded={openId === ticket.ticketId} onClick={(e) => { e.stopPropagation(); onNode(ticket.ticketId); }}
                data-testid={`series-node-${ticket.ticketId}`}>
                <span className="sr-node-id">
                  <span>{ticket.key}{alsoIn.length > 0 ? ` ${t("series.graph.alsoIn", { repos: alsoIn.join(" + ") })}` : ""}</span>
                  {pr && <span>{t("series.merge.pr", { number: pr.number })}</span>}
                </span>
                <span className="sr-node-name">{ticket.title}</span>
              </button>
            );
          })}
        </div>
      </div>
      {open && at && menu(open.ticket, at)}
    </section>
  );
}
