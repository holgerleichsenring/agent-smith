"use client";

import { Fragment, useEffect, useMemo, useRef } from "react";
import { useT } from "@/hooks/useT";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { seriesTicketBadges, type SeriesBadge } from "./seriesCiState";
import { SERIES_NODE_KINDS, seriesNeededChain } from "./seriesNodeKind";
import { SeriesRepoChips, seriesChipLabels } from "./SeriesRepoChips";
import { NODE_H, NODE_W, seriesNodeId, seriesNodeLinksId, seriesOrderLayout } from "./seriesOrderLayout";

// 2026-11-23-f562: the Overview's order graph — repo lanes, depth columns, every ticket in ONE colour by its hold
// (ticket.kind, the server's), edges orthogonal and the needed-merge chain's edges in the accent, and a legend.
// Clicking a ticket opens its details (drawn by the page, outside the scrolling box so it is never clipped); the graph
// scrolls sideways in its own box, and scrolling it closes them. 2026-11-25-9992j: a ticket a run works on now carries the
// working mark, and each node its host's marks (conflict, CI).
// 2026-11-27-ecf2a: the course select is gone — a row of the rail's list lights its ticket's course instead: the course's
// edges take the path stroke, the pointed-at node an outline, and the box scrolls to it. Pointing at the graph does nothing.

interface Props {
  detail: SeriesRunDetail;
  /** The tickets lit — the pointed-at row's course; null lights everything. */
  lit: Set<string> | null;
  /** 2026-11-27-ecf2a: the ticket whose row is pointed at; the graph scrolls to it while no details are open. */
  hovered?: string | null;
  openId: string | null;
  onNode: (ticketId: string, node: HTMLElement) => void;
  onClose: () => void;
  /** 2026-11-26-b186: folds the graph to one line; absent, no button. */
  onHide?: () => void;
}

// 2026-11-25-9992j: the small marks a node carries for its host — a conflict, red CI, CI running, a build not started.
const MARK_TONE: Record<SeriesBadge, string> = { conflict: "bad", approval: "person", ciRed: "bad", ciRunning: "run", ciNotStarted: "need" };

/** 2026-11-25-9992j: a run is at work on it now — the ticket's own flag or the series' workingOn, both the server's. */
function isWorkedOn(detail: SeriesRunDetail, ticket: SeriesRunTicket): boolean {
  return ticket.working || detail.workingOn.includes(ticket.key);
}

/** 2026-11-27-ecf2a: brings the node into the box sideways (centred, as the mock) and into the overview's height when it is
 *  outside it — only when it is not in view already, so moving along the list does not shake the graph, and at once: a
 *  smooth scroll still running would close the details a click on the row opens meanwhile. */
function scrollToNode(box: HTMLElement, ticketId: string) {
  const node = document.getElementById(seriesNodeId(ticketId));
  if (!node) return;
  const left = node.offsetLeft;
  if (left < box.scrollLeft || left + NODE_W > box.scrollLeft + box.clientWidth)
    box.scrollTo?.({ left: Math.max(0, left - box.clientWidth / 2 + NODE_W / 2), behavior: "auto" });
  const overview = box.closest<HTMLElement>(".sr-overview");
  if (!overview || overview.scrollHeight <= overview.clientHeight) return;
  const own = node.getBoundingClientRect();
  const view = overview.getBoundingClientRect();
  const off = own.top < view.top ? own.top - view.top - 8 : own.bottom > view.bottom ? own.bottom - view.bottom + 8 : 0;
  if (off !== 0) overview.scrollBy?.({ top: off, behavior: "auto" });
}

export function SeriesGraph({ detail, lit, hovered = null, openId, onNode, onClose, onHide }: Props) {
  const t = useT();
  const layout = useMemo(() => seriesOrderLayout(detail), [detail]);
  const chain = useMemo(() => seriesNeededChain(detail), [detail]);
  const labels = useMemo(() => seriesChipLabels(detail), [detail]);
  // 2026-11-27-ecf2b: the legend's two sample chips carry the series' own first labels, one letter when it has only one repo.
  const legend = [Object.values(labels)[0] ?? "A", Object.values(labels)[1] ?? Object.values(labels)[0] ?? "A"];
  const on = (id: string) => !lit || lit.has(id);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (hovered && !openId && box.current) scrollToNode(box.current, hovered);
  }, [hovered, openId]);
  return (
    <section className="sr-graph-panel" aria-label={t("series.graph.title")} title={t("series.graph.hint")}>
      {onHide && (
        <div className="sr-graph-head">
          <button type="button" className="d-link" aria-expanded={true} onClick={onHide} data-testid="series-graph-toggle">
            {t("series.graph.hide")}</button>
        </div>
      )}
      <div ref={box} className="sr-graph-box" data-testid="series-graph" onScroll={() => { if (openId) onClose(); }}>
        <div className="sr-graph" style={{ width: layout.width, height: layout.height }}>
          {layout.lanes.map((lane) => (
            <div key={lane.repo ?? ""} className="sr-lane" style={{ top: lane.top }} data-testid={`series-lane-${lane.repo ?? "none"}`}>
              <span>{lane.repo ?? t("series.graph.noRepo")}</span>
            </div>
          ))}
          <svg width={layout.width} height={layout.height} aria-hidden="true">
            {layout.lines.map(({ edge, d }) => {
              const needed = chain.has(edge.ticketId) && chain.has(edge.requiredTicketId);
              const course = on(edge.ticketId) && on(edge.requiredTicketId);
              return (
                <path key={`${edge.requiredTicketId}>${edge.ticketId}`} d={d}
                  className={`sr-edge${needed ? " needed" : ""}${!course ? " dim" : lit ? " on" : ""}`}
                  data-testid={`series-edge-${edge.requiredTicketId}>${edge.ticketId}`} />
              );
            })}
          </svg>
          {layout.nodes.map(({ ticket, x, y }) => {
            const kind = ticket.kind;
            const working = isWorkedOn(detail, ticket);
            const marks = seriesTicketBadges(ticket);
            const said = [t(`series.kind.${kind}`), working ? t("series.graph.working") : null, ...marks.map((m) => t(`series.badge.${m}`))];
            const dim = on(ticket.ticketId) ? "" : " dim";
            return (
              <Fragment key={ticket.ticketId}>
              <button id={seriesNodeId(ticket.ticketId)} type="button" style={{ left: x, top: y, width: NODE_W, height: NODE_H }}
                className={`sr-node ${kind}${working ? " working" : ""}${dim}${hovered === ticket.ticketId ? " hovered" : ""}`} data-kind={kind}
                aria-label={`${ticket.key} ${ticket.title}: ${said.filter(Boolean).join(", ")}`} aria-haspopup="dialog"
                aria-expanded={openId === ticket.ticketId} onClick={(e) => { e.stopPropagation(); onNode(ticket.ticketId, e.currentTarget); }}
                data-testid={`series-node-${ticket.ticketId}`} data-working={working || undefined}>
                <span className="sr-node-id">
                  <span>{ticket.key}</span>
                  {/* the links row's room, held so the key ends in an ellipsis before it as it did */}
                  <span className="sr-node-marks sr-node-room" aria-hidden="true">
                    {marks.map((mark) => <i key={mark} className="sr-mark" />)}
                    <SeriesRepoChips ticket={ticket} labels={labels} still />
                    {ticket.webUrl && <span>↗</span>}
                  </span>
                </span>
                <span className="sr-node-name">{ticket.title}</span>
              </button>
              {/* 2026-11-26-5201e: the node's marks and its links — the pull request and the tracker, each in a new tab — sit
                  over the node's key row, beside its button rather than inside it. 2026-11-27-ecf2b: a chip per pull request. */}
              <span id={seriesNodeLinksId(ticket.ticketId)} className={`sr-node-links${dim}`} style={{ left: x, top: y, width: NODE_W }} data-testid={`series-links-${ticket.ticketId}`}>
                {marks.map((mark) => <i key={mark} className={`sr-mark ${MARK_TONE[mark]}`} title={t(`series.badge.${mark}`)}
                  data-testid={`series-mark-${mark}-${ticket.ticketId}`} />)}
                <SeriesRepoChips ticket={ticket} labels={labels} />
                {ticket.webUrl && <a href={ticket.webUrl} target="_blank" rel="noreferrer" aria-label={t("series.node.openTracker", { key: ticket.key })}
                  title={t("series.node.openTracker", { key: ticket.key })} data-testid={`series-links-tracker-${ticket.ticketId}`}>↗</a>}
              </span>
              </Fragment>
            );
          })}
        </div>
      </div>
      {/* 2026-11-26-b186: the legend under the graph, so a bounded overview shows the graph first */}
      <div className="sr-legend" data-testid="series-graph-legend">
        {SERIES_NODE_KINDS.map((kind) => <span key={kind}><i className={`sr-dot ${kind}`} />{t(`series.kind.${kind}`)}</span>)}
        <span className="sr-legend-chips" data-testid="series-legend-chips">
          <b className="sr-chip code">{legend[0]}</b>{t("series.legend.code")} · <b className="sr-chip">{legend[1]}</b>{t("series.legend.record")}
        </span>
      </div>
    </section>
  );
}
